using UnityEngine;

/// <summary>
/// AuraVT — AvatarConfig
/// ScriptableObject persisting avatar preferences between sessions.
/// Create via: Assets/AuraVT/Resources/AvatarConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "AuraVT/Avatar Config", fileName = "AvatarConfig")]
public class AvatarConfig : ScriptableObject
{
    [Header("Last Session")]
    public string lastLoadedPath = "";
    public Vector3 avatarPosition  = Vector3.zero;
    public Vector3 avatarRotation  = Vector3.zero;
    public float   avatarScale     = 1.0f;

    [Header("Display")]
    [Tooltip("Camera vertical FOV in degrees.")]
    public float cameraFOV = 30f;
    [Tooltip("Camera Y offset from avatar root (in world units).")]
    public float cameraHeightOffset = 1.5f;
    [Tooltip("Camera Z distance from avatar.")]
    public float cameraDistance = 2.5f;

    [Header("Performance")]
    [Tooltip("Max triangles before LOD reduction is triggered.")]
    public int   maxTriangleCount = 60000;
    [Tooltip("Enable spring bone physics simulation.")]
    public bool  enableSpringBones = true;
    [Tooltip("Spring bone update rate (Hz). Lower = cheaper.")]
    [Range(15, 60)]
    public int   springBoneUpdateHz = 30;

    [Header("Auto-load")]
    [Tooltip("Automatically reload last avatar on startup.")]
    public bool autoLoadLastAvatar = true;
}
