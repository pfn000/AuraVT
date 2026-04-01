using UnityEngine;
using UniVRM10;

/// <summary>
/// AuraVT — IdleAnimationController
///
/// Drives the avatar with subtle life-like animations when face tracking
/// is inactive or lost. Runs in parallel — BlendshapeDriver enables/disables it.
///
/// Animations:
///   - Auto-blink (randomised interval, natural sinusoidal curve)
///   - Micro head sway (very gentle sine oscillation on Y and Z)
///   - Breathing (subtle chest scale + spine lean)
///   - Idle mouth micro-movement (very slight 'aa' to suggest breathing)
/// </summary>
public class IdleAnimationController : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────────
    [Header("Blink")]
    [SerializeField] private FaceTrackingConfig config;
    [SerializeField] private float blinkSpeed     = 8f;    // How fast the blink closes/opens

    [Header("Head Sway")]
    [SerializeField] private float swayAmplitudeY  = 1.5f;  // degrees
    [SerializeField] private float swayAmplitudeZ  = 0.8f;
    [SerializeField] private float swaySpeedY      = 0.4f;  // Hz
    [SerializeField] private float swaySpeedZ      = 0.27f;

    [Header("Breathing")]
    [SerializeField] private float breathAmplitude = 0.003f; // Scale change on chest
    [SerializeField] private float breathSpeed     = 0.25f;  // Hz (~15 breaths/min)

    [Header("Idle Mouth")]
    [SerializeField] private float idleMouthAmplitude = 0.012f;
    [SerializeField] private float idleMouthSpeed     = 0.22f;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool         _active;
    private AvatarManager _avatarManager;
    private Vrm10Instance _vrm;
    private Transform    _headBone;
    private Transform    _spineBone;
    private Quaternion   _headRestRot  = Quaternion.identity;
    private Quaternion   _spineRestRot = Quaternion.identity;

    // Blink state machine
    private enum BlinkState { Waiting, Closing, Opening }
    private BlinkState _blinkState   = BlinkState.Waiting;
    private float      _blinkTimer   = 0f;
    private float      _nextBlinkIn  = 3f;
    private float      _blinkWeight  = 0f;

    // Phase offsets for organic feel
    private float _swayOffsetY, _swayOffsetZ, _breathOffset, _mouthOffset;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _avatarManager = FindObjectOfType<AvatarManager>();
        if (_avatarManager != null)
        {
            _avatarManager.OnAvatarLoaded   += OnAvatarLoaded;
            _avatarManager.OnAvatarUnloaded += OnAvatarUnloaded;
        }

        // Random phase offsets so avatars don't look identical
        _swayOffsetY  = Random.Range(0f, Mathf.PI * 2f);
        _swayOffsetZ  = Random.Range(0f, Mathf.PI * 2f);
        _breathOffset = Random.Range(0f, Mathf.PI * 2f);
        _mouthOffset  = Random.Range(0f, Mathf.PI * 2f);

        _nextBlinkIn  = RandomBlinkInterval();
    }

    void OnDestroy()
    {
        if (_avatarManager != null)
        {
            _avatarManager.OnAvatarLoaded   -= OnAvatarLoaded;
            _avatarManager.OnAvatarUnloaded -= OnAvatarUnloaded;
        }
    }

    void Update()
    {
        if (!_active || _vrm == null) return;

        float t = Time.time;

        UpdateBlink(t);
        UpdateHeadSway(t);
        UpdateBreathing(t);
        UpdateIdleMouth(t);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetActive(bool active)
    {
        _active = active;
        if (!active) ResetAll();
    }

    // ── Blink ─────────────────────────────────────────────────────────────────

    private void UpdateBlink(float t)
    {
        var expr = _vrm?.Runtime?.Expression;
        if (expr == null) return;

        _blinkTimer += Time.deltaTime;

        switch (_blinkState)
        {
            case BlinkState.Waiting:
                if (_blinkTimer >= _nextBlinkIn)
                {
                    _blinkState = BlinkState.Closing;
                    _blinkTimer = 0f;
                    _blinkWeight = 0f;
                }
                break;

            case BlinkState.Closing:
                _blinkWeight += Time.deltaTime * blinkSpeed;
                if (_blinkWeight >= 1f)
                {
                    _blinkWeight = 1f;
                    _blinkState  = BlinkState.Opening;
                }
                break;

            case BlinkState.Opening:
                _blinkWeight -= Time.deltaTime * blinkSpeed;
                if (_blinkWeight <= 0f)
                {
                    _blinkWeight  = 0f;
                    _blinkState   = BlinkState.Waiting;
                    _blinkTimer   = 0f;
                    _nextBlinkIn  = RandomBlinkInterval();
                }
                break;
        }

        // Natural blink curve (sin² smooths the hard edges)
        float weight = Mathf.Sin(_blinkWeight * Mathf.PI * 0.5f);
        weight = weight * weight;

        try
        {
            expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.blink), weight);
        }
        catch { }
    }

    // ── Head sway ─────────────────────────────────────────────────────────────

    private void UpdateHeadSway(float t)
    {
        if (_headBone == null) return;

        float yaw  = Mathf.Sin(t * swaySpeedY * Mathf.PI * 2f + _swayOffsetY) * swayAmplitudeY;
        float roll = Mathf.Sin(t * swaySpeedZ * Mathf.PI * 2f + _swayOffsetZ) * swayAmplitudeZ;

        _headBone.localRotation = Quaternion.Slerp(
            _headBone.localRotation,
            _headRestRot * Quaternion.Euler(0f, yaw, roll),
            Time.deltaTime * 4f
        );
    }

    // ── Breathing (subtle spine) ──────────────────────────────────────────────

    private void UpdateBreathing(float t)
    {
        if (_spineBone == null) return;

        float breath = Mathf.Sin(t * breathSpeed * Mathf.PI * 2f + _breathOffset);
        float leanX  = breath * 0.4f;   // very subtle forward/back lean in degrees

        _spineBone.localRotation = Quaternion.Slerp(
            _spineBone.localRotation,
            _spineRestRot * Quaternion.Euler(leanX, 0f, 0f),
            Time.deltaTime * 3f
        );
    }

    // ── Idle mouth micro-movement ─────────────────────────────────────────────

    private void UpdateIdleMouth(float t)
    {
        var expr = _vrm?.Runtime?.Expression;
        if (expr == null) return;

        float val = Mathf.Abs(Mathf.Sin(t * idleMouthSpeed * Mathf.PI * 2f + _mouthOffset))
                    * idleMouthAmplitude;
        try
        {
            expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.aa),
                           Mathf.Clamp01(val));
        }
        catch { }
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    private void ResetAll()
    {
        _blinkWeight = 0f;
        _blinkState  = BlinkState.Waiting;
        _blinkTimer  = 0f;

        if (_headBone  != null) _headBone.localRotation  = _headRestRot;
        if (_spineBone != null) _spineBone.localRotation = _spineRestRot;

        var expr = _vrm?.Runtime?.Expression;
        if (expr == null) return;
        try
        {
            expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.blink), 0f);
            expr.SetWeight(ExpressionKey.CreateFromPreset(ExpressionPreset.aa),    0f);
        }
        catch { }
    }

    // ── Avatar events ─────────────────────────────────────────────────────────

    private void OnAvatarLoaded(AvatarInstance instance)
    {
        _vrm = instance.VrmInstance;
        if (_vrm == null) return;

        _headBone  = _vrm.Runtime?.ControlRig?.GetBoneTransform(HumanBodyBones.Head);
        _spineBone = _vrm.Runtime?.ControlRig?.GetBoneTransform(HumanBodyBones.Spine);

        if (_headBone  != null) _headRestRot  = _headBone.localRotation;
        if (_spineBone != null) _spineRestRot = _spineBone.localRotation;

        SetActive(true);
    }

    private void OnAvatarUnloaded()
    {
        _vrm       = null;
        _headBone  = null;
        _spineBone = null;
        _active    = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private float RandomBlinkInterval()
    {
        float min = config?.blinkIntervalMin ?? 2f;
        float max = config?.blinkIntervalMax ?? 6f;
        return Random.Range(min, max);
    }
}
