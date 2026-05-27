#region Imports
using UnityEngine;
#endregion

/// <summary>
/// Visualizes 3D linear acceleration using dynamic volumetric meshes. 
/// Relies on a decoupled SensorTelemetryProvider for raw network data.
/// </summary>
public class VolumetricVisualizer : MonoBehaviour
{
    #region Configuration Fields
    [Header("Dependencies")]
    [Tooltip("The data source providing the raw acceleration vectors.")]
    [SerializeField] private SensorTelemetryProvider telemetryProvider;

    [Header("Volumetric Visualization")]
    [Tooltip("Assign your custom Blender arrow prefab here. Must be modeled along the Y-axis (Up).")]
    public GameObject arrowPrefab;

    [Tooltip("The maximum expected raw acceleration value (used for scaling).")]
    public float maxAcceleration = 2000f;

    [Tooltip("The maximum physical length of the volumetric arrows in Unity meters.")]
    public float maxArrowLength = 1f;

    [Tooltip("The resting thickness multiplier. 1.0 means it uses your Blender model's native thickness.")]
    public float baseThickness = 0.25f;

    [Header("Animation & Filtering")]
    [Tooltip("How fast the arrows interpolate to the new values. Higher = snappier, Lower = smoother.")]
    public float interpolationSpeed = 15f;

    [Tooltip("Minimum force required on an axis to display the arrow. Hides completely if below this value.")]
    public float minDisplayThreshold = 200f;

    [Header("Visibility Toggles")]
    [Tooltip("Show the individual X (Red), Y (Green), and Z (Blue) component vectors.")]
    public bool showAxisVectors = true;

    [Tooltip("Show the combined net acceleration vector (Yellow).")]
    public bool showNetVector = true;
    #endregion

    #region Private Fields
    // The smoothed visual target
    private Vector3 _currentSmoothedAcceleration = Vector3.zero;

    // Instantiated arrow objects
    private GameObject _arrowX;
    private GameObject _arrowY;
    private GameObject _arrowZ;
    private GameObject _arrowNet;
    #endregion

    #region Initialization
    /// <summary>
    /// Initializes the procedural arrows at startup.
    /// </summary>
    private void Start()
    {
        InitializeArrows();
    }

    /// <summary>
    /// Creates four arrow instances (3 axes + 1 net vector).
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
        _arrowNet = CreateAndColorArrow("Vector_Net (Yellow)", Color.yellow);
    }

    /// <summary>
    /// Instantiates the prefab, tints the materials, and defaults to hidden.
    /// </summary>
    /// <param name="objName">The name to assign to the new GameObject.</param>
    /// <param name="tintColor">The color to apply to all child renderers.</param>
    /// <returns>The instantiated GameObject.</returns>
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

    #region Unity Update Loop
    /// <summary>
    /// Reads the raw data from the provider, interpolates it for smooth animation, and updates the meshes.
    /// </summary>
    private void Update()
    {
        if (telemetryProvider == null) return;

        // Safely fetch the latest raw data from our decoupled provider
        Vector3 targetAccel = telemetryProvider.RawAcceleration;

        // Lerp glides the current value towards the target value based on Time.deltaTime
        _currentSmoothedAcceleration = Vector3.Lerp(_currentSmoothedAcceleration, targetAccel, interpolationSpeed * Time.deltaTime);

        // Pass the smoothed data into the component visualizers
        UpdateComponentArrow(_arrowX, _currentSmoothedAcceleration.x, transform.right, showAxisVectors);
        UpdateComponentArrow(_arrowY, _currentSmoothedAcceleration.y, transform.up, showAxisVectors);
        UpdateComponentArrow(_arrowZ, _currentSmoothedAcceleration.z, transform.forward, showAxisVectors);

        // Update the net combined vector visualizer
        UpdateNetArrow(_arrowNet, _currentSmoothedAcceleration, showNetVector);
    }

    /// <summary>
    /// Evaluates the threshold, scales, and rotates an individual component axis arrow.
    /// </summary>
    /// <param name="arrow">The arrow GameObject to update.</param>
    /// <param name="forceValue">The force applied along this specific axis.</param>
    /// <param name="axisDirection">The physical world direction of this axis.</param>
    /// <param name="isVisible">Whether this axis is toggled on in the UI.</param>
    private void UpdateComponentArrow(GameObject arrow, float forceValue, Vector3 axisDirection, bool isVisible)
    {
        if (arrow == null) return;

        // If toggled off or below threshold, hide completely
        if (!isVisible || Mathf.Abs(forceValue) < minDisplayThreshold)
        {
            if (arrow.activeSelf) arrow.SetActive(false);
            return;
        }

        if (!arrow.activeSelf) arrow.SetActive(true);

        float magnitude = Mathf.Clamp01(Mathf.Abs(forceValue) / maxAcceleration);

        // Clamped minimum length protects geometry normals and lighting
        float length = Mathf.Max(0.01f, magnitude * maxArrowLength);
        float thickness = baseThickness + (magnitude * 0.1f); 

        Vector3 targetDirection = forceValue >= 0 ? axisDirection : -axisDirection;

        arrow.transform.localScale = new Vector3(thickness, length, thickness);
        
        if (targetDirection != Vector3.zero) 
        {
            arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, targetDirection);
        }
    }

    /// <summary>
    /// Evaluates the threshold, scales, and rotates the combined net force arrow in full 3D space.
    /// </summary>
    /// <param name="arrow">The arrow GameObject to update.</param>
    /// <param name="netForce">The combined 3D force vector.</param>
    /// <param name="isVisible">Whether the net vector is toggled on in the UI.</param>
    private void UpdateNetArrow(GameObject arrow, Vector3 netForce, bool isVisible)
    {
        if (arrow == null) return;

        float magnitudeRaw = netForce.magnitude;

        // If toggled off or the combined force is below threshold, hide completely
        if (!isVisible || magnitudeRaw < minDisplayThreshold)
        {
            if (arrow.activeSelf) arrow.SetActive(false);
            return;
        }

        if (!arrow.activeSelf) arrow.SetActive(true);

        float magnitudeNorm = Mathf.Clamp01(magnitudeRaw / maxAcceleration);

        float length = Mathf.Max(0.01f, magnitudeNorm * maxArrowLength);
        // Make the net vector slightly thicker so it stands out from the individual axes
        float thickness = (baseThickness * 1.2f) + (magnitudeNorm * 0.15f); 

        // Convert the local force vector into the world-space direction of the parent object
        Vector3 worldNetDirection = transform.TransformDirection(netForce);

        arrow.transform.localScale = new Vector3(thickness, length, thickness);
        
        if (worldNetDirection != Vector3.zero) 
        {
            arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, worldNetDirection.normalized);
        }
    }
    #endregion
}