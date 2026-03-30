using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AuraVT — ShaderDetector
/// Scans every Renderer on an avatar GameObject and classifies
/// each material's shader into a known ShaderFamily.
/// 
/// Detection is keyword-based (shader name substring matching) —
/// no dependency on the actual shader files being present.
/// </summary>
public static class ShaderDetector
{
    // ── Shader family enum ────────────────────────────────────────────────────
    public enum ShaderFamily
    {
        Unknown,
        MToon,          // VRM standard (MToon 0.x and MToon 1.0)
        Poiyomi,        // Poiyomi Toon 7.x / 8.x
        LilToon,        // lilToon 1.x
        URPLit,         // Unity URP/Lit — already compatible
        URPUnlit,       // Unity URP/Unlit — already compatible
        StandardLit,    // Legacy Built-in Standard — needs upgrade
        AuraVT,         // Already remapped by us — skip
    }

    // ── Detection keyword table ───────────────────────────────────────────────
    // Order matters: more specific entries first.
    private static readonly (string keyword, ShaderFamily family)[] _detectionTable =
    {
        ("AuraVT/",                     ShaderFamily.AuraVT),
        ("VRM/MToon",                   ShaderFamily.MToon),
        ("UniGLTF/UniUnlit",            ShaderFamily.MToon),   // UniVRM unlit variant
        (".poiyomi",                    ShaderFamily.Poiyomi),
        ("Poiyomi/",                    ShaderFamily.Poiyomi),
        ("Hidden/Poiyomi",              ShaderFamily.Poiyomi),
        ("liltoon",                     ShaderFamily.LilToon),
        ("lilToon",                     ShaderFamily.LilToon),
        ("_lil/",                       ShaderFamily.LilToon),
        ("Universal Render Pipeline/Lit",   ShaderFamily.URPLit),
        ("Universal Render Pipeline/Unlit", ShaderFamily.URPUnlit),
        ("Standard",                    ShaderFamily.StandardLit),
    };

    // ── Result type ───────────────────────────────────────────────────────────
    public struct DetectionResult
    {
        public Renderer  Renderer;
        public Material  Material;
        public int       MaterialIndex;
        public ShaderFamily Family;
        public string    ShaderName;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scan all Renderers on avatarRoot and return one DetectionResult per material slot.
    /// </summary>
    public static List<DetectionResult> ScanAvatar(GameObject avatarRoot)
    {
        var results = new List<DetectionResult>();
        if (avatarRoot == null) return results;

        foreach (var renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
        {
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null || mat.shader == null) continue;

                results.Add(new DetectionResult
                {
                    Renderer      = renderer,
                    Material      = mat,
                    MaterialIndex = i,
                    Family        = Classify(mat.shader.name),
                    ShaderName    = mat.shader.name,
                });
            }
        }

        LogSummary(results);
        return results;
    }

    /// <summary>
    /// Classify a shader name string into a ShaderFamily.
    /// </summary>
    public static ShaderFamily Classify(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName)) return ShaderFamily.Unknown;

        // Case-insensitive substring search
        foreach (var (keyword, family) in _detectionTable)
        {
            if (shaderName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return family;
        }
        return ShaderFamily.Unknown;
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    private static void LogSummary(List<DetectionResult> results)
    {
        var counts = new Dictionary<ShaderFamily, int>();
        foreach (var r in results)
        {
            if (!counts.ContainsKey(r.Family)) counts[r.Family] = 0;
            counts[r.Family]++;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[AuraVT] ShaderDetector scan complete:");
        foreach (var kv in counts)
            sb.AppendLine($"  {kv.Key}: {kv.Value} material(s)");

        Debug.Log(sb.ToString());
    }
}
