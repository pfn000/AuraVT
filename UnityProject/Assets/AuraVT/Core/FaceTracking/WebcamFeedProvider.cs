using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// AuraVT — WebcamFeedProvider
///
/// Manages the Unity WebCamTexture, selects the best available webcam,
/// and provides pre-processed RenderTexture frames ready for ML inference.
///
/// Output: 192×192 RGB RenderTexture (MediaPipe face mesh input size).
/// Runs at a configurable FPS independent of the render loop to save CPU.
/// </summary>
public class WebcamFeedProvider : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────────
    [Header("Webcam")]
    [Tooltip("Leave empty to use the default system webcam.")]
    public string preferredDeviceName = "";

    [Tooltip("Webcam capture resolution. Higher = more accurate but slower.")]
    public int captureWidth  = 640;
    public int captureHeight = 480;

    [Tooltip("Webcam capture FPS.")]
    public int captureFPS = 30;

    [Header("Inference Input")]
    [Tooltip("Size expected by the face landmark ONNX model (MediaPipe = 192).")]
    public int inferenceSize = 192;

    [Tooltip("How many webcam frames to process per second through the ML model. " +
             "Lower = cheaper on CPU/GPU. 15 is smooth enough for VTubing.")]
    [Range(5, 30)]
    public int inferenceRate = 15;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<RenderTexture> OnFrameReady;
    public bool IsRunning => _webcam != null && _webcam.isPlaying;

    // ── Internals ─────────────────────────────────────────────────────────────
    private WebCamTexture  _webcam;
    private RenderTexture  _inferenceRT;
    private Material       _blitMat;        // For flipping + normalizing
    private float          _frameInterval;
    private float          _frameTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public IEnumerator StartWebcam()
    {
        // Request camera permission on supported platforms
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogError("[AuraVT] WebcamFeedProvider: Camera permission denied.");
            yield break;
        }

        // Select device
        string deviceName = SelectDevice();

        _webcam = new WebCamTexture(deviceName, captureWidth, captureHeight, captureFPS);
        _webcam.Play();

        // Wait for webcam to actually start delivering frames
        float timeout = 5f;
        float elapsed = 0f;
        while (!_webcam.didUpdateThisFrame && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!_webcam.didUpdateThisFrame)
        {
            Debug.LogError("[AuraVT] WebcamFeedProvider: Webcam failed to start.");
            yield break;
        }

        // Create inference RenderTexture
        _inferenceRT = new RenderTexture(inferenceSize, inferenceSize, 0,
                                          RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };
        _inferenceRT.Create();

        _frameInterval = 1f / Mathf.Max(1, inferenceRate);

        Debug.Log($"[AuraVT] Webcam started: {_webcam.deviceName} " +
                  $"({_webcam.width}×{_webcam.height} @ {captureFPS}fps)");
    }

    public void StopWebcam()
    {
        if (_webcam != null)
        {
            _webcam.Stop();
            Destroy(_webcam);
            _webcam = null;
        }
        if (_inferenceRT != null)
        {
            _inferenceRT.Release();
            Destroy(_inferenceRT);
            _inferenceRT = null;
        }
    }

    void Update()
    {
        if (!IsRunning || _inferenceRT == null) return;
        if (!_webcam.didUpdateThisFrame) return;

        _frameTimer += Time.deltaTime;
        if (_frameTimer < _frameInterval) return;
        _frameTimer -= _frameInterval;

        PrepareInferenceFrame();
        OnFrameReady?.Invoke(_inferenceRT);
    }

    void OnDestroy() => StopWebcam();

    // ── Frame preparation ─────────────────────────────────────────────────────

    private void PrepareInferenceFrame()
    {
        // WebCamTexture may be vertically flipped depending on platform.
        // We blit to RenderTexture using Graphics.Blit with a flip matrix.
        bool needFlip = _webcam.videoVerticallyMirrored;

        if (needFlip)
        {
            // Flip UV Y axis: map [0,1] → [1,0]
            var prev = RenderTexture.active;
            RenderTexture.active = _inferenceRT;
            GL.PushMatrix();
            GL.LoadOrtho();
            GL.invertCulling = true;
            Graphics.DrawTexture(
                new Rect(0, 1, 1, -1),  // Flipped Y
                _webcam);
            GL.invertCulling = false;
            GL.PopMatrix();
            RenderTexture.active = prev;
        }
        else
        {
            Graphics.Blit(_webcam, _inferenceRT);
        }
    }

    // ── Device selection ──────────────────────────────────────────────────────

    private string SelectDevice()
    {
        var devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogWarning("[AuraVT] No webcam devices found.");
            return "";
        }

        // Prefer explicitly named device
        if (!string.IsNullOrEmpty(preferredDeviceName))
        {
            foreach (var d in devices)
                if (d.name.Contains(preferredDeviceName, StringComparison.OrdinalIgnoreCase))
                    return d.name;
        }

        // Prefer a front-facing camera
        foreach (var d in devices)
            if (d.isFrontFacing) return d.name;

        // Fallback: first device
        return devices[0].name;
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    public WebCamTexture GetWebcamTexture() => _webcam;
    public RenderTexture GetInferenceRT()   => _inferenceRT;

    /// <summary>List all available webcam device names.</summary>
    public static string[] GetDeviceNames()
    {
        var devices = WebCamTexture.devices;
        var names   = new string[devices.Length];
        for (int i = 0; i < devices.Length; i++) names[i] = devices[i].name;
        return names;
    }
}
