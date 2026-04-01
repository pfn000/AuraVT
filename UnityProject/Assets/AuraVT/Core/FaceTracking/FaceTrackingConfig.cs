using UnityEngine;

/// <summary>
/// AuraVT — FaceTrackingConfig
/// Persistent settings for face tracking sources, sensitivity, and expression mapping.
/// Create via: Assets/AuraVT/Resources/FaceTrackingConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "AuraVT/Face Tracking Config", fileName = "FaceTrackingConfig")]
public class FaceTrackingConfig : ScriptableObject
{
    [Header("Tracking Source")]
    [Tooltip("Which tracking source to use.")]
    public TrackingSource source = TrackingSource.VMC;

    [Tooltip("Fall back to idle animation if no tracking data received after this many seconds.")]
    public float trackingTimeoutSeconds = 3f;

    [Header("VMC / OSC Settings")]
    [Tooltip("UDP port to listen on for VMC protocol (VSeeFace default: 39539).")]
    public int vmcPort = 39539;

    [Header("OpenSeeFace Settings")]
    [Tooltip("Port OpenSeeFace sends JSON to (default: 11573).")]
    public int openSeeFacePort = 11573;
    [Tooltip("IP of the machine running OpenSeeFace (127.0.0.1 for local).")]
    public string openSeeFaceIP = "127.0.0.1";

    [Header("Expression Sensitivity")]
    [Range(0.1f, 2f)] public float mouthSensitivity  = 1.0f;
    [Range(0.1f, 2f)] public float eyeSensitivity     = 1.0f;
    [Range(0.1f, 2f)] public float browSensitivity    = 1.0f;
    [Range(0.1f, 2f)] public float headRotSensitivity = 1.0f;

    [Header("Smoothing")]
    [Tooltip("Expression lerp speed. Lower = smoother/slower. Range 1–30.")]
    [Range(1f, 30f)] public float smoothingSpeed = 12f;

    [Header("Blink")]
    public bool  overrideBlinkWithAuto = false;  // If true, ignore tracking blink and use idle auto-blink
    [Range(1f, 8f)] public float blinkIntervalMin = 2f;
    [Range(1f, 8f)] public float blinkIntervalMax = 6f;

    [Header("Head Rotation Limits (degrees)")]
    public float headYawLimit   = 45f;
    public float headPitchLimit = 30f;
    public float headRollLimit  = 20f;

    public enum TrackingSource { VMC, OpenSeeFace, None }
}
