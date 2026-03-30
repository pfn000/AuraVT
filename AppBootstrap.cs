using System;
using System.Collections;
using System.Management;   // Requires UnityEngine.SystemInfo; no extra ref needed in Unity
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// AuraVT — AppBootstrap
/// First MonoBehaviour to run. Detects hardware, selects quality tier,
/// initializes the transparent window, and signals other systems.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class AppBootstrap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TransparentWindow transparentWindow;
    [SerializeField] private WindowManager     windowManager;
    [SerializeField] private Camera            mainCamera;
    [SerializeField] private UniversalRenderPipelineAsset lowEndURPAsset;
    [SerializeField] private UniversalRenderPipelineAsset highEndURPAsset;

    [Header("Window")]
    [SerializeField] private int defaultWindowWidth  = 400;
    [SerializeField] private int defaultWindowHeight = 600;

    // ── Hardware thresholds ──────────────────────────────────────────────────
    private const int LOW_VRAM_MB   = 1024;   // < 1 GB VRAM = low end
    private const int MED_VRAM_MB   = 2048;
    private const int LOW_RAM_MB    = 4096;   // 4 GB system RAM

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount  = 0;   // We control frame rate manually

        // Transparent, borderless window
        Screen.SetResolution(defaultWindowWidth, defaultWindowHeight, FullScreenMode.Windowed);

        LogSystemInfo();
        SelectQualityTier();
    }

    IEnumerator Start()
    {
        // Wait one frame so the window has been created
        yield return null;

        // Camera setup for transparency
        if (mainCamera != null)
        {
            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        Debug.Log("[AuraVT] Bootstrap complete. AuraVT is ready.");
    }

    // ── Quality Tier ─────────────────────────────────────────────────────────

    private void SelectQualityTier()
    {
        int vramMB = SystemInfo.graphicsMemorySize;
        int ramMB  = SystemInfo.systemMemorySize;

        HardwareTier tier;

        if (vramMB < LOW_VRAM_MB || ramMB <= LOW_RAM_MB)
            tier = HardwareTier.Low;
        else if (vramMB < MED_VRAM_MB)
            tier = HardwareTier.Medium;
        else
            tier = HardwareTier.High;

        ApplyTier(tier);
        Debug.Log($"[AuraVT] Hardware tier detected: {tier} (VRAM: {vramMB} MB, RAM: {ramMB} MB)");
    }

    private void ApplyTier(HardwareTier tier)
    {
        switch (tier)
        {
            case HardwareTier.Low:
                QualitySettings.SetQualityLevel(0, true);
                Application.targetFrameRate = 30;
                if (lowEndURPAsset != null)
                    GraphicsSettings.defaultRenderPipeline = lowEndURPAsset;
                // Disable shadows entirely on low end
                QualitySettings.shadows         = ShadowQuality.Disable;
                QualitySettings.antiAliasing    = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                break;

            case HardwareTier.Medium:
                QualitySettings.SetQualityLevel(1, true);
                Application.targetFrameRate = 60;
                QualitySettings.shadows         = ShadowQuality.HardOnly;
                QualitySettings.antiAliasing    = 2;
                break;

            case HardwareTier.High:
                QualitySettings.SetQualityLevel(2, true);
                Application.targetFrameRate = 60;
                if (highEndURPAsset != null)
                    GraphicsSettings.defaultRenderPipeline = highEndURPAsset;
                QualitySettings.shadows         = ShadowQuality.All;
                QualitySettings.antiAliasing    = 4;
                break;
        }
    }

    private void LogSystemInfo()
    {
        Debug.Log($"[AuraVT] OS: {SystemInfo.operatingSystem}");
        Debug.Log($"[AuraVT] CPU: {SystemInfo.processorType} x{SystemInfo.processorCount}");
        Debug.Log($"[AuraVT] RAM: {SystemInfo.systemMemorySize} MB");
        Debug.Log($"[AuraVT] GPU: {SystemInfo.graphicsDeviceName}");
        Debug.Log($"[AuraVT] VRAM: {SystemInfo.graphicsMemorySize} MB");
        Debug.Log($"[AuraVT] Graphics API: {SystemInfo.graphicsDeviceType}");
    }

    public enum HardwareTier { Low, Medium, High }
}
