using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// AuraVT — DragDropHandler
///
/// Enables Windows shell drag-and-drop for .vrm and .glb files
/// onto the AuraVT overlay window.
///
/// How it works:
///   1. DragAcceptFiles() tells Windows our window accepts drops.
///   2. We hook into Unity's low-level message pump via a subclassed WndProc.
///   3. On WM_DROPFILES we read the file paths and fire the OnFilesDropped event.
///
/// NOTE: Unity doesn't expose WndProc natively, so we use a SetWindowSubclass
/// approach via a small native plugin (DragDrop.dll) that forwards WM_DROPFILES
/// messages back to managed code via a callback.
///
/// For Phase 2 we provide a pure-managed polling fallback that works in the
/// editor and doesn't require the DLL to be built yet.
/// The full native implementation is completed in Phase 6.
/// </summary>
public class DragDropHandler : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<string> OnVRMDropped;
    public event Action<string> OnGLBDropped;
    public event Action<string> OnUnsupportedDropped;

    // ── Allowed extensions ────────────────────────────────────────────────────
    private static readonly string[] SupportedExtensions = { ".vrm", ".glb", ".gltf" };

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

    // ── Win32 P/Invoke ────────────────────────────────────────────────────────
    [DllImport("shell32.dll")] private static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);
    [DllImport("shell32.dll")] private static extern uint DragQueryFile(IntPtr hDrop, uint iFile,
                                                                          StringBuilder lpszFile, uint cch);
    [DllImport("shell32.dll")] private static extern void DragFinish(IntPtr hDrop);
    [DllImport("user32.dll")]  private static extern IntPtr GetActiveWindow();

    private const int WM_DROPFILES = 0x0233;

    private IntPtr _hwnd;
    private bool   _nativeReady;

    // Managed fallback: check a static queue populated by native callback
    // (Full native subclass in Phase 6 DragDrop.dll)
    private static System.Collections.Generic.Queue<string> _pendingDrops
        = new System.Collections.Generic.Queue<string>();

    // Called by native DragDrop.dll via UnitySendMessage callback
    // (Phase 6 — already wired up in the DLL skeleton)
    public static void NativeDropCallback(string path)
    {
        _pendingDrops.Enqueue(path);
    }

    // ── Unity Editor drag-drop fallback (editor only) ─────────────────────────
#if UNITY_EDITOR
    private void OnGUI()
    {
        var e = UnityEngine.Event.current;
        if (e.type == UnityEngine.EventType.DragUpdated)
        {
            UnityEngine.DragAndDrop.visualMode = UnityEngine.DragAndDropVisualMode.Copy;
            e.Use();
        }
        else if (e.type == UnityEngine.EventType.DragPerform)
        {
            UnityEngine.DragAndDrop.AcceptDrag();
            foreach (var path in UnityEngine.DragAndDrop.paths)
                ProcessDroppedFile(path);
            e.Use();
        }
    }
#endif

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    IEnumerator Start()
    {
        // Wait one frame for window creation
        yield return null;

        _hwnd = GetActiveWindow();
        if (_hwnd != IntPtr.Zero)
        {
            DragAcceptFiles(_hwnd, true);
            _nativeReady = true;
            Debug.Log("[AuraVT] DragDropHandler: Windows drag-and-drop enabled.");
        }
        else
        {
            Debug.LogWarning("[AuraVT] DragDropHandler: Could not get HWND — drag-drop disabled.");
        }
    }

    void Update()
    {
        // Drain the static queue populated by the native callback
        while (_pendingDrops.Count > 0)
        {
            var path = _pendingDrops.Dequeue();
            ProcessDroppedFile(path);
        }
    }

    void OnDestroy()
    {
        if (_hwnd != IntPtr.Zero)
            DragAcceptFiles(_hwnd, false);
    }

#else
    // ── Linux / other platform stub ───────────────────────────────────────────
    void Start()
    {
        Debug.Log("[AuraVT] DragDropHandler: Non-Windows — " +
                  "drag-drop handled via UI file picker on this platform.");
    }

    void Update() { }
#endif

    // ── File processing (platform-agnostic) ───────────────────────────────────

    public void ProcessDroppedFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        Debug.Log($"[AuraVT] File dropped: {path}");

        switch (ext)
        {
            case ".vrm":
                OnVRMDropped?.Invoke(path);
                break;
            case ".glb":
            case ".gltf":
                OnGLBDropped?.Invoke(path);
                break;
            default:
                Debug.LogWarning($"[AuraVT] Unsupported file type: {ext}");
                OnUnsupportedDropped?.Invoke(path);
                break;
        }
    }

    /// <summary>
    /// Called from UI "Open File" button — opens a system file dialog.
    /// Uses .NET OpenFileDialog (Windows) or a simple path input (Linux).
    /// </summary>
    public void OpenFileDialog()
    {
#if UNITY_STANDALONE_WIN
        // Launch a Windows file picker on a background thread to avoid blocking
        System.Threading.Tasks.Task.Run(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Load VRM or GLB Avatar",
                Filter = "Avatar Files (*.vrm;*.glb;*.gltf)|*.vrm;*.glb;*.gltf|All Files (*.*)|*.*",
                Multiselect = false
            };
            // Must run on STA thread
            string result = null;
            var sta = new System.Threading.Thread(() =>
            {
                if (dialog.ShowDialog() == true)
                    result = dialog.FileName;
            });
            sta.SetApartmentState(System.Threading.ApartmentState.STA);
            sta.Start();
            sta.Join();
            if (!string.IsNullOrEmpty(result))
                _pendingDrops.Enqueue(result);
        });
#else
        Debug.Log("[AuraVT] File dialog not implemented on this platform yet.");
#endif
    }
}
