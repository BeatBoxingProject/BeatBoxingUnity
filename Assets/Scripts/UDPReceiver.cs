using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Globalization;

public class UDPReceiver : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = 5005;

    [Header("Mapping & Calibration")]
    [Tooltip("Multiplies the raw coordinates to make movements larger/smaller")]
    public float scaleMultiplier = 10.0f;

    [Tooltip("Adds to the position to center it in your game world")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Check these if movement is inverted")]
    public bool flipX = false;
    public bool flipY = false;
    public bool flipZ = false;

    [Header("Smoothing")]
    [Range(0, 1)] public float smoothing = 0.5f; // 1 = instant, 0.1 = very smooth/laggy

    // Private variables
    private UdpClient client;
    private Thread receiveThread;
    private Vector3 targetPosition;
    private Vector3 currentPosition;
    private bool isRunning = true;

    void Start()
    {
        // Start background thread to listen for packets
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        currentPosition = transform.position;
        targetPosition = transform.position;

        Debug.Log($"UDP Receiver listening on port {port}...");
    }

    // Runs on separate thread
    private void ReceiveData()
    {
        client = new UdpClient(port);
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                // Wait for data
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);

                // Expected format: "x,y,z"
                string[] points = text.Split(',');

                if (points.Length == 3)
                {
                    // Parse (Use InvariantCulture to handle dots vs commas in decimals)
                    float x = float.Parse(points[0], CultureInfo.InvariantCulture);
                    float y = float.Parse(points[1], CultureInfo.InvariantCulture);
                    float z = float.Parse(points[2], CultureInfo.InvariantCulture);

                    // --- MAPPING LOGIC ---
                    // 1. Flip Axes if needed
                    if (flipX) x = -x;
                    if (flipY) y = -y;
                    if (flipZ) z = -z;

                    // 2. Create Vector (OpenCV Y is often Unity Y, Z is Depth)
                    // Note: Depending on your setup, you might need to swap Y and Z here
                    // Standard OpenCV to Unity mapping is often: X -> X, Y -> -Y, Z -> Z
                    Vector3 rawPos = new Vector3(x, y, z);

                    // 3. Apply Scale and Offset
                    // We calculate this here, but apply it to the transform in Update()
                    targetPosition = (rawPos * scaleMultiplier) + offset;

                    // Debug log for verification
                    Debug.Log($"Received Position: {rawPos} => Target Position: {targetPosition}");
                }
            }
            catch (Exception err)
            {
                // Socket errors (expected when stopping)
                Debug.LogError(err.ToString());
            }
        }
    }

    // Runs every frame on Main Thread
    void Update()
    {
        // Smoothly interpolate towards the target position
        // usage: Vector3.Lerp(current, target, speed)
        currentPosition = Vector3.Lerp(currentPosition, targetPosition, 1f - smoothing);
        transform.position = currentPosition;
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (client != null) client.Close();
        if (receiveThread != null) receiveThread.Abort();
    }
}