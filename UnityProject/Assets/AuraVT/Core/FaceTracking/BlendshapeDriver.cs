using System.Collections.Generic;
using UnityEngine;
using UniVRM10;

/// <summary>
/// AuraVT — BlendshapeDriver
///
/// The central face tracking→avatar bridge.
/// Reads from OSCReceiver OR OpenSeeFaceReceiver each frame,
/// maps the raw values to VRM 1.0 expression presets with:
///   - Per-channel sensitivity multipliers
///   - Smoothed lerp (no snapping)
///   - Head bone rotation via humanoid rig
///   - Automatic fallback to IdleAnimationController when tracking drops
///
/// Attach alongside AvatarManager. Connects to the current AvatarInstance
/// via AvatarManager.OnAvatarLoaded.
/// </summary>
public class BlendshapeDriver : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────────
    [SerializeField] private FaceTrackingConfig    config;
    [SerializeField] private AvatarManager         avatarManager;
    [SerializeField] private OSCReceiver           oscReceiver;
    [SerializeField] private OpenSeeFaceReceiver   osfReceiver;
    [SerializeField] private IdleAnimationController idleController;

    // ── State ─────────────────────────────────────────────────────────────────
    private AvatarInstance   _avatar;
    private Vrm10Instance    _vrm;
    private bool             _trackingActive;

    // Smoothed expression targets (expression name → smoothed weight)
    private readonly Dictionary<string, float> _targets  = new Dictionary<string, float>();
    private readonly Dictionary<string, float> _current  = new Dictionary<string, float>();

    // Head bone reference
    private Transform _headBone;
    private Quaternion _headBoneRestRot = Quaternion.identity;

    // VMC blendshape name → VRM 1.0 ExpressionPreset map
    private static readonly Dictionary<string, string> VMC_TO_VRM = new Dictionary<string, string>
    {
        // Standard VMC → VRM 1.0 preset names
        { "Joy",      "happy"     }, { "Angry",    "angry"    },
        { "Sorrow",   "sad"       }, { "Fun",      "relaxed"  },
        { "Surprise", "surprised" }, { "Neutral",  "neutral"  },
        { "Blink",    "blink"     }, { "Blink_L",  "blinkLeft"},
        { "Blink_R",  "blinkRight"}, { "A",        "aa"       },
        { "E",        "ee"        }, { "I",        "ih"       },
        { "O",        "oh"        }, { "U",        "ou"       },
        // VRM 0.x names (auto-upgraded)
        { "LookUp",   "lookUp"    }, { "LookDown", "lookDown" },
        { "LookLeft", "lookLeft"  }, { "LookRight","lookRight"},
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (avatarManager != null)
        {
            avatarManager.OnAvatarLoaded   += OnAvatarLoaded;
            avatarManager.OnAvatarUnloaded += OnAvatarUnloaded;
        }
    }

    void Start()
    {
        if (config == null) return;

        switch (config.source)
        {
            case FaceTrackingConfig.TrackingSource.VMC:
                oscReceiver?.StartListening(config.vmcPort);
                break;
            case FaceTrackingConfig.TrackingSource.OpenSeeFace:
                osfReceiver?.StartListening(config.openSeeFaceIP, config.openSeeFacePort);
                break;
        }
    }

    void OnDestroy()
    {
        if (avatarManager != null)
        {
            avatarManager.OnAvatarLoaded   -= OnAvatarLoaded;
            avatarManager.OnAvatarUnloaded -= OnAvatarUnloaded;
        }
    }

    void Update()
    {
        if (_vrm == null) return;

        switch (config?.source)
        {
            case FaceTrackingConfig.TrackingSource.VMC:
                UpdateFromVMC();
                break;
            case FaceTrackingConfig.TrackingSource.OpenSeeFace:
                UpdateFromOSF();
                break;
        }

        CheckTrackingTimeout();
        ApplySmoothedExpressions();
        ApplyExpressionBuffer();
    }

    // ── VMC source ────────────────────────────────────────────────────────────

    private void UpdateFromVMC()
    {
        if (oscReceiver == null || !oscReceiver.DataReady) return;

        _trackingActive = true;
        idleController?.SetActive(false);

        lock (oscReceiver.BlendShapes)
        {
            foreach (var kv in oscReceiver.BlendShapes)
            {
                string vrmName = MapVMCName(kv.Key);
                _targets[vrmName] = kv.Value * GetSensitivity(vrmName);
            }
        }

        // Head bone from VMC bone data
        if (oscReceiver.Bones.TryGetValue("Head", out var headData) && _headBone != null)
        {
            _headBone.localRotation = Quaternion.Slerp(
                _headBone.localRotation,
                _headBoneRestRot * headData.rot,
                Time.deltaTime * (config?.smoothingSpeed ?? 12f)
            );
        }
    }

    // ── OpenSeeFace source ────────────────────────────────────────────────────

    private void UpdateFromOSF()
    {
        if (osfReceiver == null) return;
        if (!osfReceiver.TryGetLatest(out var fd)) return;

        _trackingActive = true;
        idleController?.SetActive(false);

        float sens = config?.eyeSensitivity ?? 1f;

        // Eye openness → blink (inverted: open=1 → blink=0)
        float blinkL = Mathf.Clamp01(1f - fd.EyeLeft  * sens);
        float blinkR = Mathf.Clamp01(1f - fd.EyeRight * sens);
        _targets["blinkLeft"]  = blinkL;
        _targets["blinkRight"] = blinkR;
        _targets["blink"]      = (blinkL + blinkR) * 0.5f;

        // Mouth
        float mSens = config?.mouthSensitivity ?? 1f;
        _targets["aa"] = Mathf.Clamp01(fd.MouthOpen * mSens * 1.5f);
        _targets["oh"] = Mathf.Clamp01(fd.MouthWide * mSens);

        // Smile / happy: mouth corners up
        float smileVal = Mathf.Clamp01(((fd.MouthCornerUpL + fd.MouthCornerUpR) * 0.5f) * mSens);
        _targets["happy"] = smileVal;

        // Brow raised → surprised
        float browSens = config?.browSensitivity ?? 1f;
        float browUp = Mathf.Clamp01(((fd.BrowUpLeft + fd.BrowUpRight) * 0.5f) * browSens);
        _targets["surprised"] = browUp;

        // Head rotation
        if (_headBone != null)
        {
            float hSens = config?.headRotSensitivity ?? 1f;
            var limits  = config != null
                ? new Vector3(config.headPitchLimit, config.headYawLimit, config.headRollLimit)
                : new Vector3(30, 45, 20);

            var euler = new Vector3(
                Mathf.Clamp(fd.HeadRotation.x * hSens, -limits.x, limits.x),
                Mathf.Clamp(fd.HeadRotation.y * hSens, -limits.y, limits.y),
                Mathf.Clamp(fd.HeadRotation.z * hSens, -limits.z, limits.z)
            );

            _headBone.localRotation = Quaternion.Slerp(
                _headBone.localRotation,
                _headBoneRestRot * Quaternion.Euler(euler),
                Time.deltaTime * (config?.smoothingSpeed ?? 12f)
            );
        }
    }

    // ── Smoothing + apply ─────────────────────────────────────────────────────

    private void ApplySmoothedExpressions()
    {
        float speed = config?.smoothingSpeed ?? 12f;
        foreach (var key in _targets.Keys)
        {
            float target = _targets[key];
            if (!_current.ContainsKey(key)) _current[key] = 0f;
            _current[key] = Mathf.Lerp(_current[key], target, Time.deltaTime * speed);
        }
    }

    private void ApplyExpressionBuffer()
    {
        if (_vrm?.Runtime?.Expression == null) return;
        foreach (var kv in _current)
        {
            try
            {
                // Try preset first
                if (System.Enum.TryParse<ExpressionPreset>(kv.Key, true, out var preset))
                {
                    _vrm.Runtime.Expression.SetWeight(
                        ExpressionKey.CreateFromPreset(preset), kv.Value);
                }
                else
                {
                    _vrm.Runtime.Expression.SetWeight(
                        ExpressionKey.CreateCustom(kv.Key), kv.Value);
                }
            }
            catch { }
        }
    }

    // ── Tracking timeout ──────────────────────────────────────────────────────

    private void CheckTrackingTimeout()
    {
        if (!_trackingActive) return;
        float timeout = config?.trackingTimeoutSeconds ?? 3f;
        float lastTime = config?.source == FaceTrackingConfig.TrackingSource.VMC
            ? oscReceiver?.LastReceiveTime ?? 0f
            : osfReceiver?.LastReceiveTime ?? 0f;

        if (Time.realtimeSinceStartup - lastTime > timeout)
        {
            _trackingActive = false;
            idleController?.SetActive(true);
            ResetAllExpressions();
            Debug.Log("[AuraVT] Tracking lost — switching to idle animation.");
        }
    }

    private void ResetAllExpressions()
    {
        foreach (var key in new List<string>(_current.Keys))
            _targets[key] = 0f;
    }

    // ── Avatar events ─────────────────────────────────────────────────────────

    private void OnAvatarLoaded(AvatarInstance instance)
    {
        _avatar = instance;
        _vrm    = instance.VrmInstance;

        if (_vrm != null)
        {
            var headTransform = _vrm.Runtime?.ControlRig?.GetBoneTransform(HumanBodyBones.Head);
            if (headTransform != null)
            {
                _headBone        = headTransform;
                _headBoneRestRot = headTransform.localRotation;
            }
        }

        _targets.Clear();
        _current.Clear();
        _trackingActive = false;
        idleController?.SetActive(true);
    }

    private void OnAvatarUnloaded()
    {
        _avatar  = null;
        _vrm     = null;
        _headBone = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string MapVMCName(string vmcName)
        => VMC_TO_VRM.TryGetValue(vmcName, out var mapped) ? mapped : vmcName.ToLower();

    private float GetSensitivity(string exprName)
    {
        if (config == null) return 1f;
        if (exprName.Contains("blink") || exprName.Contains("eye")) return config.eyeSensitivity;
        if (exprName == "aa" || exprName == "ee" || exprName == "ih" ||
            exprName == "oh" || exprName == "ou") return config.mouthSensitivity;
        if (exprName.Contains("brow")) return config.browSensitivity;
        return 1f;
    }
}
