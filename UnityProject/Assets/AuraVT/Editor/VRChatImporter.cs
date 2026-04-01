// AuraVT — VRChatImporter
// Editor-only. Orchestrates the full VRChat avatar import pipeline:
//   1. Extract .unitypackage → staging folder
//   2. Refresh AssetDatabase so Unity sees the files
//   3. Strip VRC SDK components from .prefab YAML
//   4. Refresh again so prefabs recompile clean
//   5. Remap all materials → AuraToon / URP
//   6. Find the avatar FBX / prefab and report to the window
//
// Called by VRChatImporterWindow.

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VRChatImporter
{
    public const string STAGING_ROOT = "Assets/AuraVT/_VRCImport";

    // ── Result ────────────────────────────────────────────────────────────────
    public struct ImportResult
    {
        public bool   Success;
        public string Error;
        public string StagingDir;
        public int    FilesExtracted;
        public int    VRCComponentsStripped;
        public int    MaterialsRemapped;
        public string[] PrefabPaths;    // Unity asset paths for found prefabs
        public string[] FBXPaths;       // Unity asset paths for found FBX files
    }

    // ── Main pipeline ─────────────────────────────────────────────────────────

    /// <summary>
    /// Run the full import pipeline synchronously.
    /// Should be called from a background EditorCoroutine or with
    /// EditorUtility.DisplayProgressBar to stay responsive.
    /// </summary>
    public static ImportResult Import(string unityPackagePath,
                                       Action<float, string> onProgress = null)
    {
        var result = new ImportResult();

        Report(onProgress, 0.02f, "Validating package…");

        if (!File.Exists(unityPackagePath) ||
            Path.GetExtension(unityPackagePath).ToLower() != ".unitypackage")
        {
            result.Error = "Invalid file. Please select a .unitypackage file.";
            return result;
        }

        // ── Step 1: Extract ───────────────────────────────────────────────────
        Report(onProgress, 0.10f, "Extracting package…");

        // Clear old staging data
        string stagingAbsolute = Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            STAGING_ROOT.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(stagingAbsolute))
            Directory.Delete(stagingAbsolute, recursive: true);

        var extractResult = UnityPackageExtractor.Extract(unityPackagePath, STAGING_ROOT);

        if (!extractResult.Success)
        {
            result.Error = extractResult.Error;
            return result;
        }

        result.FilesExtracted = extractResult.FBXPaths.Count +
                                extractResult.PrefabPaths.Count +
                                extractResult.TexturePaths.Count +
                                extractResult.MaterialPaths.Count;

        // ── Step 2: First AssetDatabase refresh ───────────────────────────────
        Report(onProgress, 0.30f, $"Importing {result.FilesExtracted} assets…");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // ── Step 3: Strip VRC components from prefabs ─────────────────────────
        Report(onProgress, 0.50f, "Stripping VRC SDK components…");

        var stripResult = VRCPrefabStripper.StripDirectory(stagingAbsolute);
        result.VRCComponentsStripped = stripResult.ComponentsRemoved;

        if (stripResult.ComponentsRemoved > 0)
        {
            // Refresh so Unity re-imports the stripped prefabs
            Report(onProgress, 0.60f, "Re-importing stripped prefabs…");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        // ── Step 4: Remap shaders ─────────────────────────────────────────────
        Report(onProgress, 0.72f, "Remapping shaders to AuraToon…");
        result.MaterialsRemapped = VRCShaderMapper.RemapAllMaterials(STAGING_ROOT);

        // ── Step 5: Collect output paths ──────────────────────────────────────
        Report(onProgress, 0.90f, "Finalizing…");

        result.PrefabPaths = AssetDatabase
            .FindAssets("t:Prefab", new[] { STAGING_ROOT })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        result.FBXPaths = AssetDatabase
            .FindAssets("t:Model", new[] { STAGING_ROOT })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        result.StagingDir = STAGING_ROOT;
        result.Success    = true;

        Report(onProgress, 1.0f, "Import complete!");

        Debug.Log($"[AuraVT] VRChat import complete:\n" +
                  $"  Files extracted:       {result.FilesExtracted}\n" +
                  $"  VRC components removed:{result.VRCComponentsStripped}\n" +
                  $"  Materials remapped:    {result.MaterialsRemapped}\n" +
                  $"  Prefabs found:         {result.PrefabPaths.Length}\n" +
                  $"  FBX found:             {result.FBXPaths.Length}");

        return result;
    }

    /// <summary>
    /// Ping (highlight) and select the first found prefab in the Project window.
    /// </summary>
    public static void PingFirstPrefab(ImportResult result)
    {
        if (result.PrefabPaths != null && result.PrefabPaths.Length > 0)
        {
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPaths[0]);
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
        }
        else if (result.FBXPaths != null && result.FBXPaths.Length > 0)
        {
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(result.FBXPaths[0]);
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void Report(Action<float, string> cb, float p, string msg)
    {
        cb?.Invoke(p, msg);
        EditorUtility.DisplayProgressBar("AuraVT — VRChat Importer", msg, p);
    }
}
#endif
