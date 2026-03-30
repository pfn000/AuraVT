/**
 * AuraVT — Windows Native Transparency Plugin
 * TransparentWindow.dll
 *
 * Exposes per-pixel alpha and HWND manipulation to Unity via C ABI.
 * Build: CMake + MSVC or MinGW (see CMakeLists.txt)
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <dwmapi.h>
#include <stdio.h>

// ── Export macro ─────────────────────────────────────────────────────────────
#ifdef _WIN32
    #define AURA_API __declspec(dllexport)
#else
    #define AURA_API
#endif

extern "C" {

// ── Get the foreground (Unity) HWND ──────────────────────────────────────────
AURA_API HWND AuraVT_GetWindowHandle()
{
    return GetForegroundWindow();
}

// ── Full per-pixel alpha setup ────────────────────────────────────────────────
// Call once after Unity window is created.
// Returns 0 on success, Win32 error code on failure.
AURA_API int AuraVT_EnablePerPixelAlpha(HWND hwnd)
{
    if (!hwnd) return -1;

    // Add layered + tool window styles
    LONG exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
    exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

    // Remove caption/frame via DWM glass
    MARGINS margins = { -1, -1, -1, -1 };
    HRESULT hr = DwmExtendFrameIntoClientArea(hwnd, &margins);
    if (FAILED(hr)) return (int)hr;

    return 0;
}

// ── Set window always-on-top ──────────────────────────────────────────────────
AURA_API void AuraVT_SetTopmost(HWND hwnd, BOOL topmost)
{
    HWND insertAfter = topmost ? HWND_TOPMOST : HWND_NOTOPMOST;
    SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
                 SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
}

// ── Enable/disable click-through ─────────────────────────────────────────────
AURA_API void AuraVT_SetClickThrough(HWND hwnd, BOOL enable)
{
    LONG exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
    if (enable)
        exStyle |= WS_EX_TRANSPARENT;
    else
        exStyle &= ~WS_EX_TRANSPARENT;
    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
}

// ── Move window to (x, y) screen coords ─────────────────────────────────────
AURA_API void AuraVT_SetPosition(HWND hwnd, int x, int y)
{
    SetWindowPos(hwnd, NULL, x, y, 0, 0,
                 SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
}

// ── Remove title bar / system border ─────────────────────────────────────────
AURA_API void AuraVT_RemoveBorder(HWND hwnd)
{
    LONG style   = GetWindowLong(hwnd, GWL_STYLE);
    style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZE |
               WS_MAXIMIZE | WS_SYSMENU);
    SetWindowLong(hwnd, GWL_STYLE, style);
    SetWindowPos(hwnd, NULL, 0, 0, 0, 0,
                 SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
}

// ── GPU/system info query (Phase 6 will expand this) ────────────────────────
AURA_API int AuraVT_GetSystemRAMMB()
{
    MEMORYSTATUSEX statex;
    statex.dwLength = sizeof(statex);
    GlobalMemoryStatusEx(&statex);
    return (int)(statex.ullTotalPhys / (1024 * 1024));
}

} // extern "C"
