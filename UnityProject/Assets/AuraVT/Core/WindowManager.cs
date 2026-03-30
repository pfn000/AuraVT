using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// AuraVT — WindowManager
/// Allows the user to drag the transparent overlay window by clicking
/// anywhere on the avatar (or holding a hotkey) and dragging.
/// </summary>
public class WindowManager : MonoBehaviour
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

    private const int WM_NCLBUTTONDOWN  = 0xA1;
    private const int HTCAPTION         = 0x2;

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern bool   ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll")] private static extern bool   GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern bool   SetWindowPos(IntPtr hWnd, int hWndInsertAfter,
                                                                         int x, int y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    private const uint SWP_NOSIZE      = 0x0001;
    private const uint SWP_NOZORDER    = 0x0004;
    private const uint SWP_SHOWWINDOW  = 0x0040;

    private IntPtr _hwnd;

    [Header("Drag Settings")]
    [Tooltip("Middle-mouse or this key + left-click to drag window.")]
    public Key dragModifierKey = Key.LeftAlt;

    [Tooltip("If true, dragging requires the modifier key. " +
             "If false, any left-click drag moves the window.")]
    public bool requireModifierToDrag = false;

    private bool _isDragging;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartWindowPos;

    void Awake()
    {
        _hwnd = GetActiveWindow();
    }

    void Update()
    {
        bool modifierHeld = requireModifierToDrag
            ? Keyboard.current != null && Keyboard.current[dragModifierKey].isPressed
            : true;

        // Begin drag
        if (modifierHeld && Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginWindowDrag();
        }
    }

    /// <summary>
    /// Uses Win32 HTCAPTION trick to let Windows handle the drag natively.
    /// This is far smoother than doing it in Unity Update().
    /// </summary>
    public void BeginWindowDrag()
    {
        if (_hwnd == IntPtr.Zero) return;
        ReleaseCapture();
        SendMessage(_hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
    }

    /// <summary>
    /// Snap window to a specific screen position (in pixels from top-left).
    /// </summary>
    public void SnapToPosition(int x, int y)
    {
        if (_hwnd == IntPtr.Zero) return;
        SetWindowPos(_hwnd, 0, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
    }

    /// <summary>
    /// Called by UI button — snap avatar to screen corners.
    /// </summary>
    public void SnapToCorner(Corner corner)
    {
        var res    = Screen.currentResolution;
        int winW   = Screen.width;
        int winH   = Screen.height;
        int margin = 20;

        int x, y;
        switch (corner)
        {
            case Corner.BottomRight:
                x = res.width  - winW - margin;
                y = res.height - winH - margin;
                break;
            case Corner.BottomLeft:
                x = margin;
                y = res.height - winH - margin;
                break;
            case Corner.TopRight:
                x = res.width - winW - margin;
                y = margin;
                break;
            case Corner.TopLeft:
            default:
                x = margin;
                y = margin;
                break;
        }
        SnapToPosition(x, y);
    }

    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

#else
    // Linux stub — full implementation in Phase 6 via X11/Wayland native plugin
    void Awake() => Debug.Log("[AuraVT] WindowManager: Non-Windows platform. Linux drag handled by native plugin.");
    public void BeginWindowDrag() { }
    public void SnapToCorner(object corner) { }
#endif
}
