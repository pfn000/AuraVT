using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AuraVT — AvatarManager
///
/// Singleton that coordinates the entire avatar lifecycle:
///   - Receives file paths from DragDropHandler
///   - Delegates loading to AvatarLoader
///   - Manages the "current" avatar and disposes old ones
///   - Exposes events for the UI to display progress / errors
///   - Auto-loads last avatar on startup (if config says so)
/// </summary>
public class AvatarManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static AvatarManager Instance { get; private set; }

    // ── Public Events ─────────────────────────────────────────────────────────
    public event Action<AvatarInstance>  OnAvatarLoaded;
    public event Action<float, string>   OnLoadProgress;
    public event Action<string>          OnLoadError;
    public event Action                  OnAvatarUnloaded;

    // ── Inspector References ──────────────────────────────────────────────────
    [Header("Core Components")]
    [SerializeField] private AvatarLoader    loader;
    [SerializeField] private DragDropHandler dragDrop;
    [SerializeField] private AvatarConfig    config;

    [Header("Scene")]
    [SerializeField] private Transform avatarRoot;

    // ── State ─────────────────────────────────────────────────────────────────
    public AvatarInstance CurrentAvatar { get; private set; }

    private readonly List<AvatarInstance> _history = new List<AvatarInstance>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Wire loader events
        if (loader != null)
        {
            loader.OnLoadProgress += (p, s) => OnLoadProgress?.Invoke(p, s);
            loader.OnLoadComplete += HandleLoadComplete;
            loader.OnLoadError    += HandleLoadError;
        }

        // Wire drag-drop events
        if (dragDrop != null)
        {
            dragDrop.OnVRMDropped          += LoadAvatarFromPath;
            dragDrop.OnGLBDropped          += LoadAvatarFromPath;
            dragDrop.OnUnsupportedDropped  += path =>
                OnLoadError?.Invoke($"Unsupported file: {System.IO.Path.GetExtension(path)}");
        }

        // Auto-load last avatar
        if (config != null && config.autoLoadLastAvatar &&
            !string.IsNullOrEmpty(config.lastLoadedPath) &&
            System.IO.File.Exists(config.lastLoadedPath))
        {
            Debug.Log($"[AuraVT] Auto-loading last avatar: {config.lastLoadedPath}");
            LoadAvatarFromPath(config.lastLoadedPath);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Load a new avatar from a file path.
    /// Automatically unloads the current avatar first.
    /// </summary>
    public void LoadAvatarFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (loader == null)
        {
            Debug.LogError("[AuraVT] AvatarManager: AvatarLoader reference is missing!");
            return;
        }

        if (loader.IsLoading)
        {
            Debug.Log("[AuraVT] Cancelling in-progress load for new avatar.");
            loader.CancelLoad();
        }

        // Unload current avatar (but keep history for possible undo)
        UnloadCurrentAvatar(destroyImmediate: true);

        Debug.Log($"[AuraVT] Loading avatar: {path}");
        loader.LoadAvatar(path);
    }

    /// <summary>
    /// Unload the currently displayed avatar.
    /// </summary>
    public void UnloadCurrentAvatar(bool destroyImmediate = false)
    {
        if (CurrentAvatar == null) return;

        if (!destroyImmediate)
            _history.Add(CurrentAvatar);    // keep for potential undo

        CurrentAvatar.Destroy();
        CurrentAvatar = null;
        OnAvatarUnloaded?.Invoke();
    }

    /// <summary>
    /// Scale the current avatar. Range: 0.1x – 5x.
    /// </summary>
    public void SetCurrentAvatarScale(float scale)
    {
        CurrentAvatar?.SetScale(scale);
    }

    /// <summary>
    /// Rotate the current avatar around Y axis.
    /// </summary>
    public void RotateCurrentAvatarY(float degrees)
    {
        CurrentAvatar?.RotateY(degrees);
    }

    /// <summary>
    /// Set a VRM expression on the current avatar.
    /// </summary>
    public void SetExpression(string expressionName, float weight)
    {
        CurrentAvatar?.SetExpression(expressionName, weight);
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void HandleLoadComplete(AvatarInstance instance)
    {
        CurrentAvatar = instance;
        Debug.Log($"[AuraVT] Avatar swap complete: {System.IO.Path.GetFileName(instance.SourcePath)}");
        OnAvatarLoaded?.Invoke(instance);
    }

    private void HandleLoadError(string error)
    {
        Debug.LogError($"[AuraVT] Load error: {error}");
        OnLoadError?.Invoke(error);
    }

    // ── Debug / Dev Helpers ───────────────────────────────────────────────────

    [ContextMenu("Debug: Log Current Avatar Info")]
    private void DebugLogAvatarInfo()
    {
        if (CurrentAvatar == null)
        {
            Debug.Log("[AuraVT] No avatar loaded.");
            return;
        }
        Debug.Log($"[AuraVT] Current: {CurrentAvatar.SourcePath} | " +
                  $"Type: {CurrentAvatar.Type} | " +
                  $"Scale: {CurrentAvatar.transform.localScale.x:F2}");
    }
}
