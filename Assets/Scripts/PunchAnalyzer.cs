#region Imports
using UnityEngine;
using UnityEngine.Events;
using System;
#endregion

#region Data Contracts
/// <summary>
/// A data container holding all calculated physics for a single punch event.
/// </summary>
[Serializable]
public struct PunchMetrics
{
    public float PeakAccelerationMs2;
    public float ForceNewtons;
    public Vector3 ImpactDirection;
}
#endregion

/// <summary>
/// Analyzes raw telemetry to detect impacts, calculate force (F=ma), and dispatch events.
/// </summary>
public class PunchAnalyzer : MonoBehaviour
{
    #region Configuration
    [Header("Dependencies")]
    [Tooltip("The data source for this analyzer.")]
    [SerializeField] private SensorTelemetryProvider telemetryProvider;

    [Header("Physics Constants")]
    [Tooltip("The effective body mass (in kg) transferred into the punch. (Average is 2kg - 5kg).")]
    [SerializeField] private float effectiveMassKg = 3.5f;

    [Tooltip("The BNO055 outputs 100 LSB per 1 m/s^2 in linear acceleration mode.")]
    private const float LSB_TO_MS2 = 100f;

    [Header("Detection Algorithm")]
    [Tooltip("Minimum acceleration (in m/s^2) required to trigger a punch detection. (1G = ~9.8 m/s^2)")]
    [SerializeField] private float impactThresholdMs2 = 30f; 

    [Tooltip("Time in seconds to wait before allowing another punch to be detected (prevents physical sensor ringing).")]
    [SerializeField] private float cooldownDuration = 0.4f;

    [Header("Events")]
    public UnityEvent<PunchMetrics> OnPunchLanded;
    #endregion

    #region State Machine
    private enum DetectionState { Idle, TrackingPeak, Cooldown }
    private DetectionState _currentState = DetectionState.Idle;
    
    private float _cooldownTimer = 0f;
    private float _currentPeakAccelRaw = 0f;
    private Vector3 _currentPeakDirection = Vector3.zero;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        if (!telemetryProvider) return;

        Vector3 currentAccel = telemetryProvider.RawAcceleration;
        float currentMagnitudeMs2 = currentAccel.magnitude / LSB_TO_MS2;

        ProcessStateMachine(currentAccel, currentMagnitudeMs2);
    }
    #endregion

    #region Core Logic
    /// <summary>
    /// Processes the continuous data stream to isolate distinct punch impacts.
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
                    _currentPeakAccelRaw = rawAccel.magnitude;
                    _currentPeakDirection = rawAccel.normalized;
                }
                break;

            case DetectionState.TrackingPeak:
                // Continue climbing if the force is still increasing
                if (rawAccel.magnitude > _currentPeakAccelRaw)
                {
                    _currentPeakAccelRaw = rawAccel.magnitude;
                    _currentPeakDirection = rawAccel.normalized;
                }
                // If the force drops back below threshold, the impact event is over. Calculate and fire.
                else if (magnitudeMs2 < impactThresholdMs2) 
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
    /// Calculates the final physics and broadcasts the event to the rest of the game.
    /// </summary>
    private void DispatchPunchEvent()
    {
        float peakMs2 = _currentPeakAccelRaw / LSB_TO_MS2;
        float forceNewtons = effectiveMassKg * peakMs2; // F = m * a

        PunchMetrics metrics = new PunchMetrics
        {
            PeakAccelerationMs2 = peakMs2,
            ForceNewtons = forceNewtons,
            ImpactDirection = _currentPeakDirection
        };

        // Standard Debug Logging
        Debug.Log($"[Punch Analyzer] IMPACT! Force: <b>{forceNewtons:F0} N</b> (Accel: {peakMs2:F1} m/s^2)");

        // Fire the event so UI, Audio, and Score systems can react autonomously
        OnPunchLanded?.Invoke(metrics);
    }
    #endregion
}