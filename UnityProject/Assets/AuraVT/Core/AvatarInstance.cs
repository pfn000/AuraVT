using System;
using System.Collections;
using UniVRM10;
using UnityEngine;
using UniGLTF;

/// <summary>
/// AuraVT — AvatarInstance
/// Wraps one loaded avatar (VRM or GLB) with convenience methods
/// for transform manipulation, expression control, and cleanup.
/// </summary>
public class AvatarInstance : MonoBehaviour
{
    // ── Public state ─────────────────────────────────────────────────────────
    public string       SourcePath  { get; private set; }
    public AvatarType   Type        { get; private set; }
    public bool         IsReady     { get; private set; }

    // VRM-specific handle (null for plain GLB)
    public Vrm10Instance VrmInstance { get; private set; }

    // ── Private ───────────────────────────────────────────────────────────────
    private RuntimeGltfInstance _gltfInstance;   // GLTFast handle
    private AvatarConfig        _config;
    private Camera              _mainCamera;

    // Spring bone throttle
    private float _springBoneTimer;

    // ── Initialization ───────────────────────────────────────────────────────

    public void Initialize(string path, AvatarType type,
                           Vrm10Instance vrmInstance,
                           RuntimeGltfInstance gltfInstance,
                           AvatarConfig config)
    {
        SourcePath    = path;
        Type          = type;
        VrmInstance   = vrmInstance;
        _gltfInstance = gltfInstance;
        _config       = config;
        _mainCamera   = Camera.main;
        IsReady       = true;

        ApplyDefaultTransform();
        PositionCameraForAvatar();

        Debug.Log($"[AuraVT] AvatarInstance ready: {System.IO.Path.GetFileName(path)} ({type})");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsReady || VrmInstance == null) return;

        // Throttle spring bone updates to save CPU on low-end hardware
        if (_config != null && !_config.enableSpringBones) return;

        _springBoneTimer += Time.deltaTime;
        float targetInterval = _config != null
            ? 1f / _config.springBoneUpdateHz
            : 1f / 30f;

        if (_springBoneTimer >= targetInterval)
        {
            _springBoneTimer = 0f;
            // VRM 1.0 spring bones update automatically via Vrm10Instance.Update()
            // Manual spring bone tick is only needed if we disable auto-update
        }
    }

    // ── Transform ─────────────────────────────────────────────────────────────

    private void ApplyDefaultTransform()
    {
        if (_config == null) return;
        transform.position   = _config.avatarPosition;
        transform.eulerAngles = _config.avatarRotation;
        transform.localScale  = Vector3.one * _config.avatarScale;
    }

    public void SetScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.1f, 5f);
        transform.localScale = Vector3.one * scale;
        if (_config != null) _config.avatarScale = scale;
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
        if (_config != null) _config.avatarPosition = pos;
    }

    public void RotateY(float degrees)
    {
        transform.Rotate(Vector3.up, degrees);
        if (_config != null) _config.avatarRotation = transform.eulerAngles;
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    public void PositionCameraForAvatar()
    {
        if (_mainCamera == null || _config == null) return;

        // Aim camera at approximate head height
        float headHeight = GetAvatarHeightEstimate() * _config.cameraHeightOffset;

        _mainCamera.fieldOfView  = _config.cameraFOV;
        _mainCamera.transform.position = new Vector3(
            transform.position.x,
            headHeight,
            transform.position.z - _config.cameraDistance
        );
        _mainCamera.transform.LookAt(new Vector3(
            transform.position.x,
            headHeight,
            transform.position.z
        ));
    }

    private float GetAvatarHeightEstimate()
    {
        // Try to find the head bone for VRM
        if (VrmInstance != null)
        {
            var head = VrmInstance.Runtime?.ControlRig?.GetBoneTransform(
                HumanBodyBones.Head);
            if (head != null) return head.position.y;
        }
        // GLB fallback: use renderer bounds
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds.max.y * 0.9f;
        }
        return 1.5f; // Hardcoded fallback
    }

    // ── Expressions (VRM only) ────────────────────────────────────────────────

    /// <summary>
    /// Set a VRM expression by preset name (e.g. "happy", "blink", "aa").
    /// No-op for GLB avatars.
    /// </summary>
    public void SetExpression(string expressionName, float weight)
    {
        if (VrmInstance == null) return;
        weight = Mathf.Clamp01(weight);
        try
        {
            var key = ExpressionKey.CreateFromPreset(
                (ExpressionPreset)Enum.Parse(typeof(ExpressionPreset), expressionName, true));
            VrmInstance.Runtime.Expression.SetWeight(key, weight);
        }
        catch
        {
            // Custom expression (non-preset)
            var key = ExpressionKey.CreateCustom(expressionName);
            VrmInstance.Runtime.Expression.SetWeight(key, weight);
        }
    }

    public void ResetAllExpressions()
    {
        if (VrmInstance == null) return;
        foreach (ExpressionPreset preset in Enum.GetValues(typeof(ExpressionPreset)))
        {
            try
            {
                var key = ExpressionKey.CreateFromPreset(preset);
                VrmInstance.Runtime.Expression.SetWeight(key, 0f);
            }
            catch { }
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public void Destroy()
    {
        IsReady = false;
        if (_gltfInstance != null)
        {
            _gltfInstance.Dispose();
        }
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    // ── Types ─────────────────────────────────────────────────────────────────
    public enum AvatarType { VRM, GLB, Unknown }
}
