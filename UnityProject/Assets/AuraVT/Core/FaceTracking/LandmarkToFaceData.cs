using UnityEngine;

/// <summary>
/// AuraVT — LandmarkToFaceData
///
/// Converts raw MediaPipe Face Mesh 468 landmarks to blendshape weights
/// using geometric distance calculations. No ML required for this step —
/// pure math on the landmark positions.
///
/// MediaPipe Face Mesh landmark indices (verified from MediaPipe source):
///
///   LEFT EYE (from viewer's right):
///     Upper lid: 386, 374, 373, 390, 388, 387
///     Lower lid: 362, 398, 384, 385, 386
///     Iris center: 468 (if iris landmarks enabled), else midpoint of 362–387
///
///   RIGHT EYE:
///     Upper lid: 159, 145, 144, 163, 161, 160
///     Lower lid: 33, 7, 163, 144, 145, 153
///
///   MOUTH:
///     Left corner:  61
///     Right corner: 291
///     Upper lip center: 13
///     Lower lip center: 14
///     Top of upper lip: 0
///     Bottom of lower lip: 17
///
///   EYEBROWS:
///     Left brow inner: 285  outer: 276  top: 282
///     Right brow inner: 55  outer: 46   top: 52
///
///   NOSE TIP: 1
///   CHIN:     152
///   FOREHEAD: 10
/// </summary>
public static class LandmarkToFaceData
{
    // ── Key landmark indices ──────────────────────────────────────────────────

    // Left eye (screen-left = avatar's right)
    private const int L_EYE_TOP    = 386;
    private const int L_EYE_BOT    = 374;
    private const int L_EYE_LEFT   = 362;
    private const int L_EYE_RIGHT  = 263;

    // Right eye (screen-right = avatar's left)
    private const int R_EYE_TOP    = 159;
    private const int R_EYE_BOT    = 145;
    private const int R_EYE_LEFT   = 33;
    private const int R_EYE_RIGHT  = 133;

    // Mouth
    private const int MOUTH_LEFT   = 61;
    private const int MOUTH_RIGHT  = 291;
    private const int MOUTH_TOP    = 13;
    private const int MOUTH_BOT    = 14;
    private const int UPPER_LIP    = 0;
    private const int LOWER_LIP    = 17;

    // Eyebrows
    private const int L_BROW_INNER = 285;
    private const int L_BROW_TOP   = 282;
    private const int R_BROW_INNER = 55;
    private const int R_BROW_TOP   = 52;

    // Face scale references
    private const int NOSE_TIP     = 1;
    private const int CHIN         = 152;
    private const int FOREHEAD     = 10;

    // ── Calibration state (set during first frames) ───────────────────────────
    private static float _eyeOpenRef   = -1f;   // "normal open" eye height
    private static float _mouthClosRef = -1f;   // "mouth closed" height
    private static float _browRestRef  = -1f;   // "neutral" brow height
    private static int   _calibFrames  = 0;
    private const  int   CALIB_FRAMES  = 30;    // frames to average for calibration

    private static float _eyeOpenSum, _mouthClosSum, _browRestSum;

    // ── Main conversion ───────────────────────────────────────────────────────

    /// <summary>
    /// Convert 468 normalized landmarks to OpenSeeFaceReceiver.FaceData.
    /// Call every frame from BuiltInFaceTracker.
    /// </summary>
    public static OpenSeeFaceReceiver.FaceData Convert(Vector3[] lm)
    {
        if (lm == null || lm.Length < 468)
            return default;

        var fd = new OpenSeeFaceReceiver.FaceData();

        // ── Face scale (nose-tip to chin, used for normalization) ─────────────
        float faceHeight = Vector3.Distance(lm[NOSE_TIP], lm[CHIN]);
        if (faceHeight < 0.001f) faceHeight = 0.1f;  // safety guard

        // ── Eye openness ──────────────────────────────────────────────────────
        float lEyeH = EyeAspectRatio(lm, L_EYE_TOP, L_EYE_BOT, L_EYE_LEFT, L_EYE_RIGHT);
        float rEyeH = EyeAspectRatio(lm, R_EYE_TOP, R_EYE_BOT, R_EYE_LEFT, R_EYE_RIGHT);

        // Auto-calibrate open-eye reference (first 30 frames)
        if (_calibFrames < CALIB_FRAMES)
        {
            _eyeOpenSum  += (lEyeH + rEyeH) * 0.5f;
            _calibFrames++;
            if (_calibFrames == CALIB_FRAMES)
            {
                _eyeOpenRef   = _eyeOpenSum / CALIB_FRAMES;
                Debug.Log($"[AuraVT] Eye calibration: open ref = {_eyeOpenRef:F3}");
            }
        }

        float eyeRef  = _eyeOpenRef > 0 ? _eyeOpenRef : 0.25f;
        fd.EyeLeft    = Mathf.Clamp01(lEyeH / eyeRef);
        fd.EyeRight   = Mathf.Clamp01(rEyeH / eyeRef);

        // ── Mouth open ────────────────────────────────────────────────────────
        float mouthHeight = Mathf.Abs(lm[MOUTH_TOP].y - lm[MOUTH_BOT].y);
        float mouthWidth  = Mathf.Abs(lm[MOUTH_LEFT].x - lm[MOUTH_RIGHT].x);
        fd.MouthOpen      = Mathf.Clamp01(mouthHeight / (faceHeight * 0.35f));

        // ── Mouth wide (smile) ────────────────────────────────────────────────
        // Compare corner Y positions relative to center: corners higher = smile
        float mCenterY   = (lm[UPPER_LIP].y + lm[LOWER_LIP].y) * 0.5f;
        float lCornerDY  = mCenterY - lm[MOUTH_LEFT].y;   // positive = corner above center = smile
        float rCornerDY  = mCenterY - lm[MOUTH_RIGHT].y;
        float smileScore = Mathf.Clamp01(((lCornerDY + rCornerDY) * 0.5f) / (faceHeight * 0.04f));
        fd.MouthWide         = smileScore;
        fd.MouthCornerUpL    = Mathf.Clamp01(lCornerDY / (faceHeight * 0.04f));
        fd.MouthCornerUpR    = Mathf.Clamp01(rCornerDY / (faceHeight * 0.04f));

        // ── Eyebrow raise ─────────────────────────────────────────────────────
        // Brow-to-eye distance normalized by face height
        float lBrowDist = Mathf.Abs(lm[L_BROW_TOP].y - lm[L_EYE_TOP].y);
        float rBrowDist = Mathf.Abs(lm[R_BROW_TOP].y - lm[R_EYE_TOP].y);
        float browNorm  = faceHeight * 0.12f;   // ~12% of face height = neutral brow distance

        fd.BrowUpLeft   = Mathf.Clamp01(lBrowDist / browNorm - 0.5f) * 2f;
        fd.BrowUpRight  = Mathf.Clamp01(rBrowDist / browNorm - 0.5f) * 2f;

        // Brow steepness: inner vs outer height difference
        fd.BrowSteepLeft  = Mathf.Clamp01(Mathf.Abs(lm[L_BROW_INNER].y - lm[L_BROW_TOP].y)
                                           / (faceHeight * 0.05f));
        fd.BrowSteepRight = Mathf.Clamp01(Mathf.Abs(lm[R_BROW_INNER].y - lm[R_BROW_TOP].y)
                                           / (faceHeight * 0.05f));

        // ── Head pose estimation from face geometry ────────────────────────────
        fd.HeadRotation = EstimateHeadRotation(lm, faceHeight);

        // ── Confidence: based on face size and symmetry ───────────────────────
        fd.Confidence = Mathf.Clamp01(faceHeight * 4f);   // bigger face = more confident

        fd.Timestamp = Time.realtimeSinceStartup;
        return fd;
    }

    /// <summary>Reset calibration (call when tracking restarts or avatar changes).</summary>
    public static void ResetCalibration()
    {
        _eyeOpenRef   = -1f;
        _mouthClosRef = -1f;
        _browRestRef  = -1f;
        _calibFrames  = 0;
        _eyeOpenSum   = 0f;
        _mouthClosSum = 0f;
        _browRestSum  = 0f;
        Debug.Log("[AuraVT] Face calibration reset.");
    }

    // ── Geometry helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Eye Aspect Ratio: vertical opening / horizontal width.
    /// Close to 0 = closed, ~0.3 = normal open.
    /// </summary>
    private static float EyeAspectRatio(Vector3[] lm,
                                         int top, int bot, int left, int right)
    {
        float vertical   = Mathf.Abs(lm[top].y - lm[bot].y);
        float horizontal = Mathf.Abs(lm[left].x - lm[right].x);
        return horizontal > 0.001f ? vertical / horizontal : 0f;
    }

    /// <summary>
    /// Estimate head pitch/yaw/roll from landmark geometry.
    /// Uses nose tip, chin, and eye positions to derive rotation.
    /// </summary>
    private static Vector3 EstimateHeadRotation(Vector3[] lm, float faceH)
    {
        // Yaw: horizontal asymmetry between left and right eye distances from nose
        float noseTipX  = lm[NOSE_TIP].x;
        float lEyeCX    = (lm[L_EYE_LEFT].x + lm[L_EYE_RIGHT].x) * 0.5f;
        float rEyeCX    = (lm[R_EYE_LEFT].x + lm[R_EYE_RIGHT].x) * 0.5f;
        float eyeCenter = (lEyeCX + rEyeCX) * 0.5f;
        float yaw       = (noseTipX - eyeCenter) / 0.15f * 45f;  // scale to ±45°

        // Pitch: nose tip vertical position relative to midpoint between forehead and chin
        float faceVertCenter = (lm[FOREHEAD].y + lm[CHIN].y) * 0.5f;
        float noseVertOffset = lm[NOSE_TIP].y - faceVertCenter;
        float pitch          = (noseVertOffset / (faceH * 0.5f)) * 25f;  // scale to ±25°

        // Roll: tilt of the line connecting both eye centers
        float eyeDY  = lm[L_EYE_TOP].y - lm[R_EYE_TOP].y;
        float eyeDX  = lm[L_EYE_TOP].x - lm[R_EYE_TOP].x;
        float roll   = Mathf.Atan2(eyeDY, eyeDX) * Mathf.Rad2Deg;
        roll         = Mathf.Clamp(roll, -20f, 20f);

        return new Vector3(pitch, yaw, roll);
    }
}
