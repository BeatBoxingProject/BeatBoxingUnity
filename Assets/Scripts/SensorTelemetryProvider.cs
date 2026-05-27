#region Imports
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
#endregion

#region JSON Data Structures
/// <summary>
/// Serializable class to map the 3D axis data from the Python JSON payload.
/// </summary>
[Serializable] 
public class Vector3Data 
{ 
    public float x, y, z; 
}

/// <summary>
/// Serializable class to map a single sensor's accelerometer and gyroscope data.
/// </summary>
[Serializable] 
public class SensorData 
{ 
    public Vector3Data accel, gyro; 
}

/// <summary>
/// Serializable class to map the root JSON payload containing both sensors.
/// </summary>
[Serializable] 
public class PayloadData 
{ 
    public SensorData sensor_0, sensor_1; 
}
#endregion

/// <summary>
/// Centralized network manager responsible for listening to a single UDP port, 
/// parsing the combined dual-sensor JSON payload, and exposing frame-synced, thread-safe data.
/// </summary>
public class SensorTelemetryProvider : MonoBehaviour
{
    #region Configuration
    [Header("Network Settings")]
    [Tooltip("The UDP port to listen to. Must match the Python broadcast port.")]
    public int udpPort = 5005;
    #endregion

    #region Public Data API
    /// <summary>
    /// Raw acceleration for Sensor 0 (Left Glove) straight from the hardware.
    /// </summary>
    public Vector3 LeftRawAcceleration { get; private set; } = Vector3.zero;

    /// <summary>
    /// Raw acceleration for Sensor 1 (Right Glove) straight from the hardware.
    /// </summary>
    public Vector3 RightRawAcceleration { get; private set; } = Vector3.zero;
    #endregion

    #region Private Fields
    private UdpClient _udpClient;
    private Thread _receiveThread;
    private bool _isListening = false;
    
    // Thread safety locks and buffers
    private readonly object _dataLock = new object();
    private Vector3 _threadSafeAccelLeft = Vector3.zero;
    private Vector3 _threadSafeAccelRight = Vector3.zero;
    #endregion

    #region Unity Lifecycle
    /// <summary>
    /// Initializes the UDP client and starts the background listening thread.
    /// </summary>
    private void Start()
    {
        _isListening = true;
        _receiveThread = new Thread(ReceiveUdpData) { IsBackground = true };
        _receiveThread.Start();
        
        Debug.Log($"[Safe-Strike] Central Telemetry Provider listening on Port {udpPort}...");
    }

    /// <summary>
    /// Safely transfers the background thread data to the main Unity thread once per frame.
    /// </summary>
    private void Update()
    {
        lock (_dataLock)
        {
            LeftRawAcceleration = _threadSafeAccelLeft;
            RightRawAcceleration = _threadSafeAccelRight;
        }
    }

    /// <summary>
    /// Ensures network ports and background threads are cleanly destroyed when the app closes.
    /// </summary>
    private void OnDestroy()
    {
        _isListening = false;
        
        // Safely close the socket to free the port for the OS
        _udpClient?.Close();
        
        // Terminate the background thread
        if (_receiveThread != null && _receiveThread.IsAlive) 
        {
            _receiveThread.Abort();
        }
    }
    #endregion

    #region Networking & Parsing
    /// <summary>
    /// Blocking loop running on a background thread to continuously fetch network packets.
    /// </summary>
    private void ReceiveUdpData()
    {
        try
        {
            _udpClient = new UdpClient(udpPort);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (_isListening)
            {
                byte[] data = _udpClient.Receive(ref anyIP);
                ParseAndStoreData(Encoding.UTF8.GetString(data));
            }
        }
        catch (SocketException) 
        { 
            // Silent exit during OnDestroy when the socket is intentionally closed
        }
    }

    /// <summary>
    /// Parses the JSON string and updates the thread-safe buffers simultaneously to guarantee frame-sync.
    /// </summary>
    /// <param name="jsonString">The raw JSON payload from the UDP packet.</param>
    private void ParseAndStoreData(string jsonString)
    {
        try
        {
            PayloadData payload = JsonUtility.FromJson<PayloadData>(jsonString);

            // Lock once and update both to ensure the left and right hands are never out of sync
            lock (_dataLock)
            {
                if (payload.sensor_0?.accel != null)
                {
                    _threadSafeAccelLeft = new Vector3(
                        payload.sensor_0.accel.x, 
                        payload.sensor_0.accel.y, 
                        payload.sensor_0.accel.z
                    );
                }

                if (payload.sensor_1?.accel != null)
                {
                    _threadSafeAccelRight = new Vector3(
                        payload.sensor_1.accel.x, 
                        payload.sensor_1.accel.y, 
                        payload.sensor_1.accel.z
                    );
                }
            }
        }
        catch (ArgumentException) 
        { 
            // Ignore malformed or truncated JSON packets during connection startup
        }
    }
    #endregion
}