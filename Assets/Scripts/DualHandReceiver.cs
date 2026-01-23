using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Globalization;

public class DualHandReceiver : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = 5005;

    [Header("Hand Assignments")]
    [Tooltip("Drag your Left Glove object here")]
    public Transform leftHandObject;
    [Tooltip("Drag your Right Glove object here")]
    public Transform rightHandObject;

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

    // Position targets for both hands
    private Vector3 targetPosL, currentPosL;
    private Vector3 targetPosR, currentPosR;

    private bool isRunning = true;

    void Start()
    {
        // Initialize positions to avoid jumping at start
        if (leftHandObject)
        {
            currentPosL = leftHandObject.position;
            targetPosL = leftHandObject.position;
        }
        if (rightHandObject)
        {
            currentPosR = rightHandObject.position;
            targetPosR = rightHandObject.position;
        }

        // Start background thread to listen for packets
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

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

                // Expected format: "Lx,Ly,Lz|Rx,Ry,Rz"
                // 1. Split Left and Right hands by the pipe '|' character
                string[] hands = text.Split('|');

                if (hands.Length == 2)
                {
                    // 2. Parse and Map each hand separately
                    // hands[0] is Left, hands[1] is Right
                    targetPosL = ParseAndMap(hands[0]);
                    targetPosR = ParseAndMap(hands[1]);
                }
            }
            catch (Exception err)
            {
                // Socket errors (expected when stopping)
                // specific checks can be added to ignore "Thread Aborted" noise
                Debug.LogError(err.ToString());
            }
        }
    }

    // Helper function to process raw "x,y,z" string into a Unity Vector3
    private Vector3 ParseAndMap(string rawText)
    {
        string[] points = rawText.Split(',');

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

            // 2. Create Vector
            // Note: If your Python script already swaps axes (Y/Z), we just map directly here.
            Vector3 rawPos = new Vector3(x, y, z);

            // 3. Apply Scale and Offset
            return (rawPos * scaleMultiplier) + offset;
        }

        // Return zero if parsing fails (or handle error differently)
        return Vector3.zero;
    }

    // Runs every frame on Main Thread
    void Update()
    {
        float lerpSpeed = 1f - smoothing;

        // Update Left Hand
        if (leftHandObject != null)
        {
            currentPosL = Vector3.Lerp(currentPosL, targetPosL, lerpSpeed);
            leftHandObject.position = currentPosL;
        }

        // Update Right Hand
        if (rightHandObject != null)
        {
            currentPosR = Vector3.Lerp(currentPosR, targetPosR, lerpSpeed);
            rightHandObject.position = currentPosR;
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (client != null) client.Close();
        if (receiveThread != null) receiveThread.Abort();
    }
}