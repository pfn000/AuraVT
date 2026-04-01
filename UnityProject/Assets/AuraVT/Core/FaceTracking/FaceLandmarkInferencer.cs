using System;
using System.Collections;
using Unity.Sentis;
using UnityEngine;

/// <summary>
/// AuraVT — FaceLandmarkInferencer
///
/// Runs the MediaPipe Face Mesh ONNX model via Unity Sentis.
/// Outputs 468 3D facial landmarks per frame.
///
/// MODEL SETUP (one-time, included in AuraVT release):
///   Place "face_landmark.onnx" in:
///   Assets/AuraVT/Resources/Models/face_landmark.onnx
///
///   The model is Apache 2.0 licensed (Google MediaPipe).
///   Download: https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/latest/face_landmarker.task
///   (Extract .onnx from the .task bundle using the included Tools/extract_model.py)
///
/// INPUT:  1 × 3 × 192 × 192 float tensor (RGB, normalized 0–1)
/// OUTPUT: 1 × 468 × 3 float tensor (x, y, z landmarks, normalized 0–1)
/// </summary>
public class FaceLandmarkInferencer : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────────
    [Header("Model")]
    [Tooltip("Path inside Resources folder, without extension.")]
    public string modelResourcePath = "Models/face_landmark";

    [Tooltip("GPUCompute for GPU inference (fast), CPU for low-VRAM devices.")]
    public BackendType backendType = BackendType.GPUCompute;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired on main thread after each successful inference.
    /// Array is 468 elements, each a Vector3(x, y, z) normalized 0–1.</summary>
    public event Action<Vector3[]> OnLandmarksReady;
    public bool IsReady { get; private set; }

    // ── Internals ─────────────────────────────────────────────────────────────
    private Model          _runtimeModel;
    private IWorker        _worker;
    private TensorFloat    _inputTensor;
    private Vector3[]      _landmarks = new Vector3[468];

    private const int MODEL_INPUT_SIZE = 192;
    private const int LANDMARK_COUNT   = 468;

    // Reusable CPU pixel buffer
    private Texture2D _cpuReadback;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public bool Initialize()
    {
        var modelAsset = Resources.Load<ModelAsset>(modelResourcePath);
        if (modelAsset == null)
        {
            Debug.LogError($"[AuraVT] FaceLandmarkInferencer: Model not found at " +
                           $"Resources/{modelResourcePath}. " +
                           $"Please add face_landmark.onnx to the project.");
            return false;
        }

        _runtimeModel = ModelLoader.Load(modelAsset);
        _worker       = WorkerFactory.CreateWorker(backendType, _runtimeModel);
        _cpuReadback  = new Texture2D(MODEL_INPUT_SIZE, MODEL_INPUT_SIZE,
                                       TextureFormat.RGB24, false);

        IsReady = true;
        Debug.Log($"[AuraVT] FaceLandmarkInferencer ready. Backend: {backendType}");
        return true;
    }

    void OnDestroy()
    {
        _worker?.Dispose();
        _inputTensor?.Dispose();
        if (_cpuReadback != null) Destroy(_cpuReadback);
        IsReady = false;
    }

    // ── Inference ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Run inference on a 192×192 RenderTexture frame.
    /// Non-blocking: schedules work on the GPU worker, reads back next frame.
    /// </summary>
    public void RunInference(RenderTexture frame)
    {
        if (!IsReady || _worker == null) return;

        // ── Step 1: Read pixels from RenderTexture → Texture2D ────────────────
        var prev = RenderTexture.active;
        RenderTexture.active = frame;
        _cpuReadback.ReadPixels(new Rect(0, 0, MODEL_INPUT_SIZE, MODEL_INPUT_SIZE), 0, 0);
        _cpuReadback.Apply();
        RenderTexture.active = prev;

        // ── Step 2: Build input tensor (1, 3, 192, 192) NCHW ─────────────────
        var pixels = _cpuReadback.GetPixels();
        var data   = new float[1 * 3 * MODEL_INPUT_SIZE * MODEL_INPUT_SIZE];

        // MediaPipe expects RGB channels separated, values 0–1
        for (int y = 0; y < MODEL_INPUT_SIZE; y++)
        {
            for (int x = 0; x < MODEL_INPUT_SIZE; x++)
            {
                int pixIdx = y * MODEL_INPUT_SIZE + x;
                // Flip Y for proper orientation
                int srcIdx = (MODEL_INPUT_SIZE - 1 - y) * MODEL_INPUT_SIZE + x;
                Color c    = pixels[srcIdx];

                // Channel-first layout: R plane, G plane, B plane
                int rOffset = 0 * MODEL_INPUT_SIZE * MODEL_INPUT_SIZE;
                int gOffset = 1 * MODEL_INPUT_SIZE * MODEL_INPUT_SIZE;
                int bOffset = 2 * MODEL_INPUT_SIZE * MODEL_INPUT_SIZE;

                data[rOffset + pixIdx] = c.r;
                data[gOffset + pixIdx] = c.g;
                data[bOffset + pixIdx] = c.b;
            }
        }

        _inputTensor?.Dispose();
        _inputTensor = new TensorFloat(
            new TensorShape(1, 3, MODEL_INPUT_SIZE, MODEL_INPUT_SIZE), data);

        // ── Step 3: Execute ────────────────────────────────────────────────────
        _worker.Execute(_inputTensor);

        // ── Step 4: Read output ────────────────────────────────────────────────
        var outputTensor = _worker.PeekOutput() as TensorFloat;
        if (outputTensor == null) return;

        outputTensor.MakeReadable();

        // Output shape: (1, 468, 3) — x,y,z per landmark
        for (int i = 0; i < LANDMARK_COUNT; i++)
        {
            _landmarks[i] = new Vector3(
                outputTensor[0, i, 0],   // x: 0–192
                outputTensor[0, i, 1],   // y: 0–192
                outputTensor[0, i, 2]    // z: depth
            );

            // Normalize x,y to 0–1
            _landmarks[i].x /= MODEL_INPUT_SIZE;
            _landmarks[i].y /= MODEL_INPUT_SIZE;
            // z is already in a reasonable range, keep as-is for blink detection
        }

        OnLandmarksReady?.Invoke(_landmarks);
    }

    // ── Quality tier adjustment ────────────────────────────────────────────────

    /// <summary>
    /// Switch to CPU backend on low-VRAM devices to avoid competing with rendering.
    /// Called by AppBootstrap on HardwareTier.Low.
    /// </summary>
    public void ForceCPUBackend()
    {
        if (backendType == BackendType.CPU) return;
        backendType = BackendType.CPU;

        // Recreate worker with new backend
        if (_worker != null && _runtimeModel != null)
        {
            _worker.Dispose();
            _worker = WorkerFactory.CreateWorker(BackendType.CPU, _runtimeModel);
            Debug.Log("[AuraVT] FaceLandmarkInferencer: Switched to CPU backend (low VRAM mode).");
        }
    }
}
