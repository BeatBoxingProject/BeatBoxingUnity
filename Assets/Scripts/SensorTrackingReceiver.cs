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
    public float x;
    public float y;
    public float z;
}

/// <summary>
/// Serializable class to map a single sensor's accelerometer and gyroscope data.
/// </summary>
[Serializable]
public class SensorData
{
    public Vector3Data accel;
    public Vector3Data gyro;
}

/// <summary>
/// Serializable class to map the root JSON payload containing both sensors.
/// </summary>
[Serializable]
public class PayloadData
{
    public SensorData sensor_0;
    public SensorData sensor_1;
}
#endregion

/// <summary>
/// Receives dual-sensor UDP JSON telemetry from a Raspberry Pi and visualizes 
/// the linear acceleration using debug rays in the Unity Scene View.
/// </summary>
public class SensorTrackingReceiver : MonoBehaviour
{
    #region Configuration Fields
    [Header("Network Settings")]
    [Tooltip("The UDP port to listen to (must match the Python script).")]
    public int udpPort = 5005;

    [Tooltip("Which sensor should this object visualize? (0 or 1)")]
    [Range(0, 1)]
    public int sensorIndex = 0;

    [Header("Debug Visualization")]
    [Tooltip("The maximum expected raw acceleration value (used for scaling).")]
    public float maxAcceleration = 2000f;

    [Tooltip("The maximum physical length of the debug arrows in Unity meters.")]
    public float maxArrowLength = 3f;

    [Tooltip("If true, prints the raw acceleration values to the Unity Console.")]
    public bool enableConsoleLogging = false;
    #endregion

    #region Private Fields
    private UdpClient _udpClient;
    private Thread _receiveThread;
    private bool _isListening = false;
    
    // Thread-safe storage for the latest parsed sensor data
    private Vector3 _latestAcceleration = Vector3.zero;
    private readonly object _dataLock = new object();
    #endregion

    #region Initialization
    /// <summary>
    /// Initializes the UDP client and starts the background listening thread.
    /// </summary>
    private void Start()
    {
        _isListening = true;
        _receiveThread = new Thread(new ThreadStart(ReceiveUdpData))
        {
            IsBackground = true // Ensures thread closes when Unity closes
        };
        _receiveThread.Start();
        
        Debug.Log($"[Safe-Strike] Listening for telemetry on UDP Port {udpPort} for Sensor {sensorIndex}...");
    }
    #endregion

    #region Network Threading
    /// <summary>
    /// Runs on a separate background thread to continuously listen for UDP packets 
    /// without freezing the Unity main thread.
    /// </summary>
    private void ReceiveUdpData()
    {
        try
        {
            _udpClient = new UdpClient(udpPort);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (_isListening)
            {
                // This blocks the thread until a packet arrives
                byte[] data = _udpClient.Receive(ref anyIP);
                string jsonString = Encoding.UTF8.GetString(data);

                ParseAndStoreData(jsonString);
            }
        }
        catch (SocketException ex)
        {
            if (_isListening) // Only log if we didn't intentionally close it
            {
                Debug.LogError($"[Safe-Strike] UDP Socket Error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Safe-Strike] Thread Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the JSON string and safely locks the resulting data for the main thread.
    /// </summary>
    /// <param name="jsonString">The raw JSON string from the network.</param>
    private void ParseAndStoreData(string jsonString)
    {
        try
        {
            PayloadData payload = JsonUtility.FromJson<PayloadData>(jsonString);
            SensorData targetSensor = (sensorIndex == 0) ? payload.sensor_0 : payload.sensor_1;

            if (targetSensor != null && targetSensor.accel != null)
            {
                // Lock the variable while writing so the Update() loop doesn't read partial data
                lock (_dataLock)
                {
                    // Map the python JSON to Unity's Vector3. 
                    // Note: You may need to swap axes later depending on sensor orientation!
                    _latestAcceleration = new Vector3(
                        targetSensor.accel.x,
                        targetSensor.accel.y,
                        targetSensor.accel.z
                    );
                }
            }
        }
        catch (ArgumentException)
        {
            // Ignore malformed JSON packets during startup
        }
    }
    #endregion

    #region Unity Update Loop
    /// <summary>
    /// Reads the latest thread-safe data, logs it if enabled, and draws the debug arrows.
    /// </summary>
    private void Update()
    {
        Vector3 currentAccel;

        // Briefly lock the data to safely read the latest values
        lock (_dataLock)
        {
            currentAccel = _latestAcceleration;
        }

        // --- NEW: Console Logging ---
        if (enableConsoleLogging)
        {
            // Uses "F2" formatting to limit the decimals to 2 places for readability
            Debug.Log($"[Safe-Strike] Sensor {sensorIndex} Accel: X={currentAccel.x:F2} | Y={currentAccel.y:F2} | Z={currentAccel.z:F2}");
        }

        DrawAccelerationDebugRays(currentAccel);
    }

    /// <summary>
    /// Calculates the scaled length of the arrows and draws them using Debug.DrawRay.
    /// </summary>
    /// <param name="accel">The current acceleration vector.</param>
    private void DrawAccelerationDebugRays(Vector3 accel)
    {
        Vector3 origin = transform.position;

        // Calculate lengths based on configured max values, clamped to prevent extreme glitches
        float lengthX = Mathf.Clamp(Mathf.Abs(accel.x) / maxAcceleration * maxArrowLength, 0f, maxArrowLength);
        float lengthY = Mathf.Clamp(Mathf.Abs(accel.y) / maxAcceleration * maxArrowLength, 0f, maxArrowLength);
        float lengthZ = Mathf.Clamp(Mathf.Abs(accel.z) / maxAcceleration * maxArrowLength, 0f, maxArrowLength);

        // Determine direction signs (so negative acceleration draws backwards)
        float signX = Mathf.Sign(accel.x);
        float signY = Mathf.Sign(accel.y);
        float signZ = Mathf.Sign(accel.z);

        // Draw the rays (Visible in Scene View, and Game View if "Gizmos" is enabled)
        // Red = X, Green = Y, Blue = Z (Unity standard)
        Debug.DrawRay(origin, transform.right * (lengthX * signX), Color.red);
        Debug.DrawRay(origin, transform.up * (lengthY * signY), Color.green);
        Debug.DrawRay(origin, transform.forward * (lengthZ * signZ), Color.blue);
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// Ensures the UDP socket is closed and the thread is aborted when the script 
    /// is destroyed or the application quits.
    /// </summary>
    private void OnDestroy()
    {
        _isListening = false;
        
        if (_udpClient != null)
        {
            _udpClient.Close();
        }

        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Abort();
        }
    }
    #endregion
}