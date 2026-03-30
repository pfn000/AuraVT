using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// AuraVT — TransparentWindow
/// Turns the Unity window into a click-through transparent desktop overlay.
/// Works on Windows via Win32 API (user32.dll / dwmapi.dll).
/// On other platforms, logs an informational message and does nothing.
/// </summary>
public class TransparentWindow : MonoBehaviour
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

    // ── Win32 constants ──────────────────────────────────────────────────────
    private const int GWL_EXSTYLE       = -20;
    private const int WS_EX_LAYERED     = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOPMOST     = 0x00000008;
    private const int WS_EX_TOOLWINDOW  = 0x00000080;

    private const uint LWA_COLORKEY     = 0x00000001;
    private const uint LWA_ALPHA        = 0x00000002;

    private const int HWND_TOPMOST      = -1;
    private const uint SWP_NOSIZE       = 0x0001;
    private const uint SWP_NOMOVE       = 0x0002;
    private const uint SWP_SHOWWINDOW   = 0x0040;

    // ── P/Invoke declarations ────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern int    GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int    SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool   SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool   SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    // DWM composition (required for per-pixel alpha on Win 8+)
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
    [DllImport("dwmapi.dll")] private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled(out bool pfEnabled);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public uint  dwFlags;
        public bool  fEnable;
        public IntPtr hRgnBlur;
        public bool  fTransitionOnMaximized;
    }

    // ── State ────────────────────────────────────────────────────────────────
    private IntPtr _hwnd;
    private bool   _isClickThrough;

    [Header("Overlay Settings")]
    [Tooltip("Make the window always render on top of other windows.")]
    public bool alwaysOnTop = true;

    [Tooltip("Enable click-through (mouse events pass to windows beneath).")]
    public bool clickThrough = false;

    [Tooltip("Global window opacity (0=invisible, 255=fully opaque). " +
             "Only applies when NOT using per-pixel alpha mode.")]
    [Range(0, 255)]
    public byte windowOpacity = 255;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        // Must run before any rendering. Grab HWND immediately.
        _hwnd = GetActiveWindow();
        if (_hwnd == IntPtr.Zero)
        {
            Debug.LogWarning("[AuraVT] TransparentWindow: Could not get window handle. " +
                             "Make sure the game is running in Windowed (not fullscreen) mode.");
            return;
        }

        ApplyTransparency();
        SetAlwaysOnTop(alwaysOnTop);
        SetClickThrough(clickThrough);
    }

    // Call from UI toggle
    public void SetClickThrough(bool enabled)
    {
        _isClickThrough = enabled;
        int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
        if (enabled)
            style |= WS_EX_TRANSPARENT;
        else
            style &= ~WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, style);
    }

    public void SetAlwaysOnTop(bool enabled)
    {
        alwaysOnTop = enabled;
        SetWindowPos(_hwnd,
            enabled ? HWND_TOPMOST : -2,  // -2 = HWND_NOTOPMOST
            0, 0, 0, 0,
            SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW);
    }

    public void SetWindowOpacity(byte alpha)
    {
        windowOpacity = alpha;
        SetLayeredWindowAttributes(_hwnd, 0, alpha, LWA_ALPHA);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void ApplyTransparency()
    {
        // Step 1: Add LAYERED extended style (required for transparency)
        int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
        style |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
        SetWindowLong(_hwnd, GWL_EXSTYLE, style);

        // Step 2: Attempt DWM per-pixel alpha (Windows 8+, requires Aero)
        bool dwmEnabled;
        DwmIsCompositionEnabled(out dwmEnabled);

        if (dwmEnabled)
        {
            // Extend glass frame to cover the entire client area
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1,
                                         cyTopHeight  = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        }
        else
        {
            // Fallback: color-key transparency (magenta = transparent)
            SetLayeredWindowAttributes(_hwnd, 0xFF00FF, 0, LWA_COLORKEY);
            Debug.Log("[AuraVT] DWM composition disabled — using color-key transparency fallback.");
        }

        // Step 3: Tell Unity's camera to render with alpha=0 background
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);  // Fully transparent
        }

        Debug.Log("[AuraVT] Transparent window applied.");
    }

#else
    // ── Non-Windows stub ─────────────────────────────────────────────────────
    void Awake()
    {
        Debug.Log("[AuraVT] TransparentWindow: Platform not Windows. " +
                  "Transparency will be handled by the Linux native plugin in Phase 6.");
    }
#endif
}
