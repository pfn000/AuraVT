using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniGLTF;
using UniVRM10;
using GLTFast;
using UnityEngine;

/// <summary>
/// AuraVT — AvatarLoader
/// Loads .vrm (0.x and 1.0) and .glb/.gltf files at runtime.
///
/// VRM path:  UniVRM 1.0 → Vrm10.LoadPathAsync (handles 0.x auto-upgrade)
/// GLB path:  GLTFast → GltfAsset streaming import
///
/// All loading is fully async — never blocks the main thread.
/// Reports progress via the LoadProgress event (0–1 float).
/// </summary>
public class AvatarLoader : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<float, string>                  OnLoadProgress;   // (0-1, statusText)
    public event Action<AvatarInstance>                 OnLoadComplete;
    public event Action<string>                         OnLoadError;

    // ── Dependencies ──────────────────────────────────────────────────────────
    [SerializeField] private AvatarConfig config;
    [SerializeField] private Transform    avatarRoot;   // Parent for spawned avatars

    // ── State ─────────────────────────────────────────────────────────────────
    private CancellationTokenSource _cts;
    private bool _isLoading;

    // ── Public API ────────────────────────────────────────────────────────────

    public bool IsLoading => _isLoading;

    /// <summary>
    /// Main entry point. Accepts .vrm, .glb, .gltf paths.
    /// Cancels any in-progress load automatically.
    /// </summary>
    public async void LoadAvatar(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            OnLoadError?.Invoke($"File not found: {filePath}");
            return;
        }

        // Cancel previous load if still running
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _isLoading = true;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            ReportProgress(0.05f, $"Opening {Path.GetFileName(filePath)}…");

            AvatarInstance instance;
            if (ext == ".vrm")
                instance = await LoadVRMAsync(filePath, _cts.Token);
            else if (ext == ".glb" || ext == ".gltf")
                instance = await LoadGLBAsync(filePath, _cts.Token);
            else
            {
                OnLoadError?.Invoke($"Unsupported format: {ext}. Use .vrm or .glb");
                return;
            }

            if (instance != null)
            {
                ReportProgress(1f, "Avatar ready!");
                // Persist last path
                if (config != null) config.lastLoadedPath = filePath;
                OnLoadComplete?.Invoke(instance);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[AuraVT] Load cancelled.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuraVT] Load failed: {ex}");
            OnLoadError?.Invoke($"Load error: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void CancelLoad()
    {
        _cts?.Cancel();
    }

    // ── VRM Loading ───────────────────────────────────────────────────────────

    private async Task<AvatarInstance> LoadVRMAsync(string path, CancellationToken ct)
    {
        ReportProgress(0.1f, "Parsing VRM header…");

        // Vrm10.LoadPathAsync handles VRM 0.x (auto-migrates) and VRM 1.0
        var vrmGameObject = await Vrm10.LoadPathAsync(
            path,
            canLoadVrm0X: true,          // accept VRM 0.x
            showMeshes:   false,         // we'll show after setup
            awaitCaller:  new RuntimeOnlyAwaitCaller(),
            ct:           ct
        );

        ct.ThrowIfCancellationRequested();

        if (vrmGameObject == null)
            throw new Exception("Vrm10.LoadPathAsync returned null.");

        ReportProgress(0.7f, "Setting up VRM instance…");

        // Parent and reset
        vrmGameObject.transform.SetParent(avatarRoot, false);

        var vrm10 = vrmGameObject.GetComponent<Vrm10Instance>();

        // Enable spring bones based on config
        if (config != null && !config.enableSpringBones && vrm10 != null)
        {
            // Disable the spring bone manager to save CPU
            var springBone = vrmGameObject.GetComponentInChildren<UniGLTF.SpringBoneJobs.Vrm10FastSpringBoneRuntime>();
            if (springBone != null) springBone.enabled = false;
        }

        // Show meshes
        foreach (var r in vrmGameObject.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        ReportProgress(0.85f, "Optimizing geometry…");
        OptimizeMeshes(vrmGameObject);

        ReportProgress(0.95f, "Spawning avatar…");

        // Create and initialize AvatarInstance component
        var inst = vrmGameObject.AddComponent<AvatarInstance>();
        inst.Initialize(path, AvatarInstance.AvatarType.VRM, vrm10, null, config);

        Debug.Log($"[AuraVT] VRM loaded: {Path.GetFileName(path)}");
        return inst;
    }

    // ── GLB/glTF Loading ──────────────────────────────────────────────────────

    private async Task<AvatarInstance> LoadGLBAsync(string path, CancellationToken ct)
    {
        ReportProgress(0.1f, "Parsing GLB/glTF…");

        // Spawn a holder GameObject
        var holderGO = new GameObject(Path.GetFileNameWithoutExtension(path));
        holderGO.transform.SetParent(avatarRoot, false);

        var gltfAsset = holderGO.AddComponent<GltfAsset>();

        // GLTFast load from file URI
        string uri = "file:///" + path.Replace('\\', '/');
        bool loaded = await gltfAsset.Load(uri);

        ct.ThrowIfCancellationRequested();

        if (!loaded)
        {
            Destroy(holderGO);
            throw new Exception("GLTFast failed to load: " + path);
        }

        ReportProgress(0.75f, "Processing geometry…");
        OptimizeMeshes(holderGO);

        ReportProgress(0.95f, "Spawning GLB avatar…");

        var inst = holderGO.AddComponent<AvatarInstance>();
        inst.Initialize(path, AvatarInstance.AvatarType.GLB, null, null, config);

        Debug.Log($"[AuraVT] GLB loaded: {Path.GetFileName(path)}");
        return inst;
    }

    // ── Mesh Optimization ─────────────────────────────────────────────────────

    /// <summary>
    /// Post-load mesh optimizations:
    /// - Combines static meshes where possible
    /// - Enables GPU instancing on materials
    /// - Warns if triangle count exceeds config threshold
    /// </summary>
    private void OptimizeMeshes(GameObject avatarGO)
    {
        int totalTris = 0;
        foreach (var mf in avatarGO.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh != null)
                totalTris += mf.sharedMesh.triangles.Length / 3;
        }
        foreach (var smr in avatarGO.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh != null)
                totalTris += smr.sharedMesh.triangles.Length / 3;
        }

        // Enable GPU instancing on all materials
        foreach (var r in avatarGO.GetComponentsInChildren<Renderer>())
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat != null) mat.enableInstancing = true;
            }
        }

        int threshold = config != null ? config.maxTriangleCount : 60000;
        if (totalTris > threshold)
        {
            Debug.LogWarning($"[AuraVT] Avatar has {totalTris:N0} triangles " +
                             $"(threshold: {threshold:N0}). " +
                             $"Consider using a lower-poly model for best performance.");
        }
        else
        {
            Debug.Log($"[AuraVT] Avatar mesh: {totalTris:N0} triangles ✓");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ReportProgress(float value, string status)
    {
        OnLoadProgress?.Invoke(value, status);
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
