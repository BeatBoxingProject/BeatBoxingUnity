#region Imports
using UnityEngine;
using UnityEngine.Events;
using System;
#endregion

#region Shared Enums
/// <summary>
/// Shared enumeration to distinguish between the left and right hardware sensors.
/// </summary>
public enum HandSide 
{ 
    Left, 
    Right 
}

/// <summary>
/// Defines the mathematical approach used to score the power of a punch.
/// </summary>
public enum PowerCalculationMethod
{
    PeakForce,
    AreaUnderCurve
}
#endregion

#region Data Contracts
/// <summary>
/// A data container holding all calculated physics for a single punch event.
/// </summary>
[Serializable]
public struct PunchMetrics
{
    public HandSide Hand;
    public float PeakAccelerationMs2;
    public float PeakForceNewtons;
    public float ImpulseNewtonSeconds;
    public float FinalPunchScore;
    public Vector3 ImpactDirection;
}
#endregion

/// <summary>
/// Analyzes raw telemetry to detect impacts, calculate physics (Peak Force vs Impulse), 
/// and filters out shadowboxing noise using an integration time window.
/// </summary>
public class PunchAnalyzer : MonoBehaviour
{
    #region Configuration
    [Header("Dependencies")]
    [Tooltip("The centralized data source for this analyzer.")]
    [SerializeField] private SensorTelemetryProvider telemetryProvider;

    [Tooltip("Which hand's telemetry should this analyzer process?")]
    [SerializeField] private HandSide trackedHand = HandSide.Left;

    [Header("Physics Constants")]
    [Tooltip("The effective body mass (in kg) transferred into the punch. (Average is 2kg - 5kg).")]
    [SerializeField] private float effectiveMassKg = 3.5f;

    [Tooltip("The BNO055 outputs 100 LSB per 1 m/s^2 in linear acceleration mode.")]
    private const float LSB_TO_MS2 = 100f;

    [Header("Detection Algorithm")]
    [Tooltip("Minimum acceleration (in m/s^2) required to trigger a punch detection. (1G = ~9.8 m/s^2)")]
    [SerializeField] private float impactThresholdMs2 = 30f; 

    [Tooltip("The maximum time (in seconds) to accumulate area. A real heavy bag impact is ~0.06s. This prevents shadowboxing from scoring high.")]
    [SerializeField] private float maxIntegrationTime = 0.06f;

    [Tooltip("Time in seconds to wait before allowing another punch to be detected.")]
    [SerializeField] private float cooldownDuration = 0.4f;

    [Header("Scoring")]
    [Tooltip("AreaUnderCurve (Impulse) correctly rewards heavy bag punches when combined with the integration window.")]
    [SerializeField] private PowerCalculationMethod scoringMethod = PowerCalculationMethod.AreaUnderCurve;

    [Header("Events")]
    public UnityEvent<PunchMetrics> OnPunchLanded;
    #endregion

    #region State Machine
    private enum DetectionState { Idle, TrackingPeak, Cooldown }
    private DetectionState _currentState = DetectionState.Idle;
    
    private float _cooldownTimer = 0f;
    private float _impactTimer = 0f;
    
    // Physics tracking variables
    private float _currentPeakAccelRaw = 0f;
    private float _accumulatedVelocityMs = 0f;
    private Vector3 _currentPeakDirection = Vector3.zero;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        if (telemetryProvider == null) return;

        // Fetch the correct data based on the configured hand side
        Vector3 currentAccel = (trackedHand == HandSide.Left) 
            ? telemetryProvider.LeftRawAcceleration 
            : telemetryProvider.RightRawAcceleration;

        float currentMagnitudeMs2 = currentAccel.magnitude / LSB_TO_MS2;

        ProcessStateMachine(currentAccel, currentMagnitudeMs2);
    }
    #endregion

    #region Core Logic
    /// <summary>
    /// Processes the continuous data stream to isolate distinct punch impacts and accumulate physics data.
    /// </summary>
    private void ProcessStateMachine(Vector3 rawAccel, float magnitudeMs2)
    {
        switch (_currentState)
        {
            case DetectionState.Idle:
                // Trigger: Acceleration spikes above the impact threshold
                if (magnitudeMs2 >= impactThresholdMs2)
                {
                    _currentState = DetectionState.TrackingPeak;
                    
                    // Initialize physics tracking for the new impact
                    _currentPeakAccelRaw = rawAccel.magnitude;
                    _currentPeakDirection = rawAccel.normalized;
                    _accumulatedVelocityMs = magnitudeMs2 * Time.deltaTime;
                    _impactTimer = Time.deltaTime;
                }
                break;

            case DetectionState.TrackingPeak:
                // Accumulate the time we have spent in this specific impact
                _impactTimer += Time.deltaTime;

                // IMPORTANT FILTER: Only integrate Area Under Curve for the true physical impact window (~60ms).
                // This ignores the long, drawn-out muscle retraction of hitting the air.
                if (_impactTimer <= maxIntegrationTime)
                {
                    _accumulatedVelocityMs += magnitudeMs2 * Time.deltaTime;
                }

                // Always track the peak, even if it happens slightly after the window
                if (rawAccel.magnitude > _currentPeakAccelRaw)
                {
                    _currentPeakAccelRaw = rawAccel.magnitude;
                    _currentPeakDirection = rawAccel.normalized;
                }
                
                // If the force drops back below threshold, the impact event is completely over. Calculate and fire.
                if (magnitudeMs2 < impactThresholdMs2) 
                {
                    DispatchPunchEvent();
                    
                    _currentState = DetectionState.Cooldown;
                    _cooldownTimer = cooldownDuration;
                }
                break;

            case DetectionState.Cooldown:
                // Prevent sensor bounce/ringing from triggering multiple phantom punches
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer <= 0)
                {
                    _currentState = DetectionState.Idle;
                }
                break;
        }
    }

    /// <summary>
    /// Compiles the tracked metrics, determines the final score, and dispatches the event.
    /// </summary>
    private void DispatchPunchEvent()
    {
        float peakMs2 = _currentPeakAccelRaw / LSB_TO_MS2;
        float forceNewtons = effectiveMassKg * peakMs2;                   
        float impulseNs = effectiveMassKg * _accumulatedVelocityMs;       

        float finalScore = (scoringMethod == PowerCalculationMethod.PeakForce) ? forceNewtons : impulseNs;
        string scoreLabel = (scoringMethod == PowerCalculationMethod.PeakForce) ? "Newtons" : "N*s (Impulse)";

        PunchMetrics metrics = new PunchMetrics
        {
            Hand = trackedHand,
            PeakAccelerationMs2 = peakMs2,
            PeakForceNewtons = forceNewtons,
            ImpulseNewtonSeconds = impulseNs,
            FinalPunchScore = finalScore,
            ImpactDirection = _currentPeakDirection
        };

        Debug.Log($"[{trackedHand} Analyzer] IMPACT! Score: <b>{finalScore:F0} {scoreLabel}</b> | Peak: {forceNewtons:F0} N | Area: {impulseNs:F0} N*s");

        OnPunchLanded?.Invoke(metrics);
    }
    #endregion
}