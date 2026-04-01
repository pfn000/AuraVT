using System.Collections;
using UnityEngine;

/// <summary>
/// AuraVT — BuiltInFaceTracker
///
/// Top-level coordinator for the zero-dependency built-in face tracking pipeline:
///
///   WebcamFeedProvider → FaceLandmarkInferencer → LandmarkToFaceData → BlendshapeDriver
///
/// Attach to the AvatarSystem GameObject alongside BlendshapeDriver.
/// Set FaceTrackingConfig.source = BuiltIn to activate.
///
/// This is the "works out of the box" mode — no external software required.
/// The user only needs a webcam and to allow camera permission on first launch.
/// </summary>
public class BuiltInFaceTracker : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────────
    [Header("Pipeline Components")]
    [SerializeField] private FaceTrackingConfig      config;
    [SerializeField] private WebcamFeedProvider      webcamProvider;
    [SerializeField] private FaceLandmarkInferencer  inferencer;
    [SerializeField] private BlendshapeDriver        blendshapeDriver;

    [Header("Preview (optional)")]
    [Tooltip("Assign a RawImage UI element to preview the webcam feed.")]
    [SerializeField] private UnityEngine.UI.RawImage webcamPreview;
    [SerializeField] private bool showPreviewByDefault = false;

    // ── State ─────────────────────────────────────────────────────────────────
    public bool IsRunning     { get; private set; }
    public bool IsCalibrating { get; private set; }

    // Shared face data between inference callback and BlendshapeDriver
    private OpenSeeFaceReceiver.FaceData _latestFaceData;
    private bool _dataReady;
    private float _lastDataTime;
    private readonly object _dataLock = new object();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (config?.source != FaceTrackingConfig.TrackingSource.BuiltIn) return;
        StartCoroutine(InitializePipeline());
    }

    void Update()
    {
        if (!IsRunning) return;

        // Push latest face data to BlendshapeDriver each frame
        lock (_dataLock)
        {
            if (_dataReady)
            {
                PushToBlendshapeDriver(_latestFaceData);
                _dataReady = false;
            }
        }
    }

    void OnDestroy() => Shutdown();

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartTracking()  => StartCoroutine(InitializePipeline());
    public void StopTracking()   => Shutdown();

    public void Recalibrate()
    {
        LandmarkToFaceData.ResetCalibration();
        Debug.Log("[AuraVT] Face tracking recalibration started (hold neutral expression for 2s).");
    }

    public void SetPreviewVisible(bool visible)
    {
        if (webcamPreview != null)
            webcamPreview.gameObject.SetActive(visible);
    }

    public void TogglePreview() =>
        SetPreviewVisible(webcamPreview != null && !webcamPreview.gameObject.activeSelf);

    // ── Pipeline initialization ───────────────────────────────────────────────

    private IEnumerator InitializePipeline()
    {
        Debug.Log("[AuraVT] BuiltInFaceTracker: Initializing…");
        IsCalibrating = true;

        // ── Step 1: Initialize ONNX model ─────────────────────────────────────
        if (inferencer == null)
            inferencer = gameObject.AddComponent<FaceLandmarkInferencer>();

        // Switch to CPU backend on low-end hardware
        if (AppBootstrap.CurrentTier == AppBootstrap.HardwareTier.Low)
            inferencer.ForceCPUBackend();

        bool modelReady = inferencer.Initialize();
        if (!modelReady)
        {
            Debug.LogError("[AuraVT] Face landmark model failed to load. " +
                           "Check that face_landmark.onnx is in Resources/Models/.");
            IsCalibrating = false;
            yield break;
        }

        // ── Step 2: Start webcam ───────────────────────────────────────────────
        if (webcamProvider == null)
            webcamProvider = gameObject.AddComponent<WebcamFeedProvider>();

        // Apply config inference rate
        if (config != null)
            webcamProvider.inferenceRate = config.source == FaceTrackingConfig.TrackingSource.BuiltIn
                ? 15 : 15;  // 15 FPS inference — smooth enough, low CPU cost

        yield return StartCoroutine(webcamProvider.StartWebcam());

        if (!webcamProvider.IsRunning)
        {
            Debug.LogError("[AuraVT] Webcam failed to start.");
            IsCalibrating = false;
            yield break;
        }

        // ── Step 3: Wire events ────────────────────────────────────────────────
        webcamProvider.OnFrameReady    += OnFrameReady;
        inferencer.OnLandmarksReady    += OnLandmarksReady;

        // ── Step 4: Set up webcam preview ─────────────────────────────────────
        if (webcamPreview != null)
        {
            webcamPreview.texture = webcamProvider.GetWebcamTexture();
            SetPreviewVisible(showPreviewByDefault);
        }

        IsRunning     = true;
        IsCalibrating = true;  // Still calibrating until LandmarkToFaceData finishes

        // Wait for calibration (30 frames at 15fps ≈ 2 seconds)
        yield return new WaitForSeconds(2.2f);
        IsCalibrating = false;

        Debug.Log("[AuraVT] BuiltInFaceTracker: Ready ✓");
    }

    // ── Frame pipeline ────────────────────────────────────────────────────────

    // Called by WebcamFeedProvider at inferenceRate Hz
    private void OnFrameReady(RenderTexture frame)
    {
        inferencer.RunInference(frame);
    }

    // Called by FaceLandmarkInferencer after each inference (main thread)
    private void OnLandmarksReady(Vector3[] landmarks)
    {
        var faceData = LandmarkToFaceData.Convert(landmarks);

        lock (_dataLock)
        {
            _latestFaceData  = faceData;
            _dataReady       = true;
            _lastDataTime    = Time.realtimeSinceStartup;
        }
    }

    // Push converted face data directly into BlendshapeDriver's OSF pipeline
    private void PushToBlendshapeDriver(OpenSeeFaceReceiver.FaceData fd)
    {
        if (blendshapeDriver == null) return;
        blendshapeDriver.InjectBuiltInFaceData(fd);
    }

    // ── Shutdown ──────────────────────────────────────────────────────────────

    private void Shutdown()
    {
        if (webcamProvider != null)
        {
            webcamProvider.OnFrameReady -= OnFrameReady;
            webcamProvider.StopWebcam();
        }
        if (inferencer != null)
            inferencer.OnLandmarksReady -= OnLandmarksReady;

        IsRunning     = false;
        IsCalibrating = false;
        Debug.Log("[AuraVT] BuiltInFaceTracker: Stopped.");
    }
}
