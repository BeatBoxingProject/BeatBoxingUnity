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
/// the linear acceleration using custom Blender prefabs with smooth interpolation.
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

    [Header("Volumetric Visualization")]
    [Tooltip("Assign your custom Blender arrow prefab here. Must be modeled along the Y-axis (Up).")]
    public GameObject arrowPrefab;

    [Tooltip("The maximum expected raw acceleration value (used for scaling).")]
    public float maxAcceleration = 2000f;

    [Tooltip("The maximum physical length of the volumetric arrows in Unity meters.")]
    public float maxArrowLength = 3f;

    [Tooltip("The resting thickness multiplier. 1.0 means it uses your Blender model's native thickness.")]
    public float baseThickness = 1.0f;

    [Header("Animation & Filtering")]
    [Tooltip("How fast the arrows interpolate to the new values. Higher = snappier, Lower = smoother.")]
    public float interpolationSpeed = 15f;

    [Tooltip("Minimum force required on an axis to display the arrow. Hides completely if below this value.")]
    public float minDisplayThreshold = 200f;

    [Tooltip("If true, prints the raw acceleration values to the Unity Console.")]
    public bool enableConsoleLogging = false;
    #endregion

    #region Private Fields
    private UdpClient _udpClient;
    private Thread _receiveThread;
    private bool _isListening = false;
    
    // Thread-safe storage for the raw network data
    private Vector3 _latestAccelerationRaw = Vector3.zero;
    private readonly object _dataLock = new object();

    // The smoothed visual target
    private Vector3 _currentSmoothedAcceleration = Vector3.zero;

    // Instantiated arrow objects
    private GameObject _arrowX;
    private GameObject _arrowY;
    private GameObject _arrowZ;
    #endregion

    #region Initialization
    /// <summary>
    /// Initializes the arrows directly and starts the network thread.
    /// </summary>
    private void Start()
    {
        InitializeArrows();

        _isListening = true;
        _receiveThread = new Thread(new ThreadStart(ReceiveUdpData))
        {
            IsBackground = true 
        };
        _receiveThread.Start();
        
        Debug.Log($"[Safe-Strike] Listening for telemetry on UDP Port {udpPort} for Sensor {sensorIndex}...");
    }

    /// <summary>
    /// Creates three arrow instances.
    /// </summary>
    private void InitializeArrows()
    {
        if (arrowPrefab == null)
        {
            Debug.LogError("[Safe-Strike] Arrow Prefab is missing! Please assign it in the Inspector.");
            return;
        }

        _arrowX = CreateAndColorArrow("Vector_X (Red)", Color.red);
        _arrowY = CreateAndColorArrow("Vector_Y (Green)", Color.green);
        _arrowZ = CreateAndColorArrow("Vector_Z (Blue)", Color.blue);
    }

    /// <summary>
    /// Instantiates the prefab and tints the materials.
    /// </summary>
    private GameObject CreateAndColorArrow(string objName, Color tintColor)
    {
        GameObject arrow = Instantiate(arrowPrefab, this.transform);
        arrow.name = objName;
        arrow.transform.localPosition = Vector3.zero;

        // Apply colors to all renderers in the prefab
        foreach (Renderer r in arrow.GetComponentsInChildren<Renderer>())
        {
            r.material.color = tintColor;
        }

        // Start hidden until acceleration passes the threshold
        arrow.SetActive(false);

        return arrow;
    }
    #endregion

    #region Network Threading
    /// <summary>
    /// Runs on a separate background thread to continuously listen for UDP packets.
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
                string jsonString = Encoding.UTF8.GetString(data);

                ParseAndStoreData(jsonString);
            }
        }
        catch (SocketException ex)
        {
            if (_isListening) Debug.LogError($"[Safe-Strike] UDP Socket Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Safe-Strike] Thread Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the JSON string and safely locks the resulting data for the main thread.
    /// </summary>
    private void ParseAndStoreData(string jsonString)
    {
        try
        {
            PayloadData payload = JsonUtility.FromJson<PayloadData>(jsonString);
            SensorData targetSensor = (sensorIndex == 0) ? payload.sensor_0 : payload.sensor_1;

            if (targetSensor != null && targetSensor.accel != null)
            {
                lock (_dataLock)
                {
                    _latestAccelerationRaw = new Vector3(
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
    /// Reads the raw data, interpolates it for smooth animation, and updates the meshes.
    /// </summary>
    private void Update()
    {
        Vector3 targetAccel;

        // Briefly lock to fetch the raw data safely
        lock (_dataLock)
        {
            targetAccel = _latestAccelerationRaw;
        }

        // --- NEW: Smooth Interpolation ---
        // Lerp glides the current value towards the target value based on Time.deltaTime, making it frame-independent
        _currentSmoothedAcceleration = Vector3.Lerp(_currentSmoothedAcceleration, targetAccel, interpolationSpeed * Time.deltaTime);

        if (enableConsoleLogging)
        {
            Debug.Log($"[Safe-Strike] Sensor {sensorIndex} Target Raw: X={targetAccel.x:F0} | Y={targetAccel.y:F0} | Z={targetAccel.z:F0}");
        }

        // Pass the smoothed data into the visualizer
        UpdateArrow(_arrowX, _currentSmoothedAcceleration.x, transform.right);
        UpdateArrow(_arrowY, _currentSmoothedAcceleration.y, transform.up);
        UpdateArrow(_arrowZ, _currentSmoothedAcceleration.z, transform.forward);
    }

    /// <summary>
    /// Evaluates the threshold, scales, and rotates the arrow along the Y-axis. 
    /// </summary>
    private void UpdateArrow(GameObject arrow, float forceValue, Vector3 axisDirection)
    {
        if (arrow == null) return;

        // --- NEW: Threshold Check ---
        // If the absolute force is below our threshold, disable the mesh entirely and stop processing
        if (Mathf.Abs(forceValue) < minDisplayThreshold)
        {
            if (arrow.activeSelf) arrow.SetActive(false);
            return;
        }

        // If it passed the threshold, ensure it is visible
        if (!arrow.activeSelf) arrow.SetActive(true);

        // Calculate normalized magnitude (0.0 to 1.0)
        float magnitude = Mathf.Clamp01(Mathf.Abs(forceValue) / maxAcceleration);

        // Calculate visual dimensions
        float length = Mathf.Max(0.01f, magnitude * maxArrowLength);
        float thickness = baseThickness + (magnitude * 0.1f); 

        // Determine target direction (flip if acceleration is negative)
        Vector3 targetDirection = forceValue >= 0 ? axisDirection : -axisDirection;

        // Apply scale and orientation hardcoded to the Y-axis of the Blender model
        arrow.transform.localScale = new Vector3(thickness, length, thickness);
        
        if (targetDirection != Vector3.zero) 
        {
            arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, targetDirection);
        }
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        _isListening = false;
        
        if (_udpClient != null) _udpClient.Close();
        if (_receiveThread != null && _receiveThread.IsAlive) _receiveThread.Abort();
    }
    #endregion
}