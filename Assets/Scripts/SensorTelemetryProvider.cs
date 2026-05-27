#region Imports
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
#endregion

#region JSON Data Structures
[Serializable] public class Vector3Data { public float x, y, z; }
[Serializable] public class SensorData { public Vector3Data accel, gyro; }
[Serializable] public class PayloadData { public SensorData sensor_0, sensor_1; }
#endregion

/// <summary>
/// Solely responsible for listening to UDP telemetry and exposing thread-safe data.
/// </summary>
public class SensorTelemetryProvider : MonoBehaviour
{
    #region Configuration
    [Header("Network Settings")]
    public int udpPort = 5005;
    [Range(0, 1)] public int sensorIndex = 0;
    #endregion

    #region Public Data API
    /// <summary>Raw acceleration straight from the sensor (Best for physics calculations).</summary>
    public Vector3 RawAcceleration { get; private set; } = Vector3.zero;
    #endregion

    #region Private Fields
    private UdpClient _udpClient;
    private Thread _receiveThread;
    private bool _isListening = false;
    private readonly object _dataLock = new object();
    private Vector3 _threadSafeAccel = Vector3.zero;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        _isListening = true;
        _receiveThread = new Thread(ReceiveUdpData) { IsBackground = true };
        _receiveThread.Start();
    }

    private void Update()
    {
        // Safely move thread data to the main-thread public property
        lock (_dataLock)
        {
            RawAcceleration = _threadSafeAccel;
        }
    }

    private void OnDestroy()
    {
        _isListening = false;
        _udpClient?.Close();
        if (_receiveThread != null && _receiveThread.IsAlive) _receiveThread.Abort();
    }
    #endregion

    #region Networking
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
        catch (SocketException) { /* Handled silent exit */ }
    }

    private void ParseAndStoreData(string jsonString)
    {
        try
        {
            PayloadData payload = JsonUtility.FromJson<PayloadData>(jsonString);
            SensorData targetSensor = (sensorIndex == 0) ? payload.sensor_0 : payload.sensor_1;

            if (targetSensor?.accel != null)
            {
                lock (_dataLock)
                {
                    _threadSafeAccel = new Vector3(targetSensor.accel.x, targetSensor.accel.y, targetSensor.accel.z);
                }
            }
        }
        catch (ArgumentException) { /* Ignore malformed JSON during startup */ }
    }
    #endregion
}