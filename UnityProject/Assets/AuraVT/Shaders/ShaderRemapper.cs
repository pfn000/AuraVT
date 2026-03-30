using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AuraVT — ShaderRemapper
///
/// Orchestrates the full shader compatibility pass:
///   1. Calls ShaderDetector.ScanAvatar() to classify all materials
///   2. Routes each material to the correct remapper (Poiyomi / lilToon / MToon / etc.)
///   3. Applies remapped materials back to the renderers
///   4. Skips already-compatible shaders (URP Lit/Unlit, AuraVT)
///
/// Called automatically by AvatarLoader after every avatar load.
/// Can also be triggered manually from the Inspector context menu.
/// </summary>
public class ShaderRemapper : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("If true, remap shaders immediately when this component is enabled.")]
    public bool remapOnEnable = false;

    [Tooltip("Print per-material remap log to Console.")]
    public bool verboseLog = true;

    // ── Tracking ──────────────────────────────────────────────────────────────
    private readonly Dictionary<Renderer, Material[]> _originalMaterials
        = new Dictionary<Renderer, Material[]>();

    private bool _remapped = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (remapOnEnable) RemapAll();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Run the full remap pass on this GameObject and all children.
    /// Safe to call multiple times — backs up originals on first call only.
    /// </summary>
    [ContextMenu("Remap Shaders Now")]
    public void RemapAll()
    {
        // Backup originals so we can restore later
        if (!_remapped)
            BackupMaterials();

        var results = ShaderDetector.ScanAvatar(gameObject);
        int remappedCount = 0;
        int skippedCount  = 0;

        foreach (var result in results)
        {
            var remapped = RemapSingle(result);

            if (remapped != null && remapped != result.Material)
            {
                // Apply the remapped material back
                ApplyMaterial(result.Renderer, result.MaterialIndex, remapped);
                remappedCount++;
                if (verboseLog)
                    Debug.Log($"[AuraVT] Remapped [{result.Family}] {result.Material.name} → {remapped.name}");
            }
            else
            {
                skippedCount++;
            }
        }

        _remapped = true;
        Debug.Log($"[AuraVT] ShaderRemapper complete: {remappedCount} remapped, {skippedCount} skipped.");
    }

    /// <summary>
    /// Restore all original materials (undo remap).
    /// </summary>
    [ContextMenu("Restore Original Shaders")]
    public void RestoreOriginals()
    {
        foreach (var kv in _originalMaterials)
        {
            if (kv.Key != null)
                kv.Key.sharedMaterials = kv.Value;
        }
        _remapped = false;
        Debug.Log("[AuraVT] Original shaders restored.");
    }

    // ── Core routing ──────────────────────────────────────────────────────────

    private Material RemapSingle(ShaderDetector.DetectionResult result)
    {
        switch (result.Family)
        {
            case ShaderDetector.ShaderFamily.Poiyomi:
                return PoiyomiRemapper.Remap(result.Material);

            case ShaderDetector.ShaderFamily.LilToon:
                return LilToonRemapper.Remap(result.Material);

            case ShaderDetector.ShaderFamily.MToon:
                // MToon is already handled by UniVRM — keep as-is for VRM avatars
                // For non-VRM GLB with MToon, apply AuraToon toon remap
                return RemapMToon(result.Material);

            case ShaderDetector.ShaderFamily.StandardLit:
                // Built-in Standard → URP Lit (basic upgrade, no toon)
                return RemapStandardToURPLit(result.Material);

            case ShaderDetector.ShaderFamily.URPLit:
            case ShaderDetector.ShaderFamily.URPUnlit:
            case ShaderDetector.ShaderFamily.AuraVT:
                // Already compatible — skip
                return null;

            case ShaderDetector.ShaderFamily.Unknown:
            default:
                Debug.LogWarning($"[AuraVT] Unknown shader: {result.ShaderName} — " +
                                 $"applying URP Lit fallback.");
                return RemapStandardToURPLit(result.Material);
        }
    }

    // ── Individual remappers ──────────────────────────────────────────────────

    private Material RemapMToon(Material source)
    {
        // MToon → AuraToon: copy key toon properties
        var toon = Shader.Find("AuraVT/AuraToon");
        if (toon == null) return null;

        var dest = new Material(toon) { name = source.name + " [AuraVT]" };

        // MToon 0.x property names
        TryCopyColor(source, dest,   "_Color",         "_BaseColor");
        TryCopyTexture(source, dest, "_MainTex",        "_BaseMap");
        TryCopyColor(source, dest,   "_ShadeColor",     "_ShadeColor");
        TryCopyFloat(source, dest,   "_ShadeShift",     "_ShadeStep");
        TryCopyFloat(source, dest,   "_ShadeToony",     "_ShadeSoftness");
        TryCopyColor(source, dest,   "_EmissionColor",  "_EmissionColor");
        TryCopyTexture(source, dest, "_EmissionMap",    "_EmissionMap");
        TryCopyFloat(source, dest,   "_OutlineWidth",   "_OutlineWidth");
        TryCopyColor(source, dest,   "_OutlineColor",   "_OutlineColor");
        TryCopyColor(source, dest,   "_RimColor",       "_RimColor");
        TryCopyFloat(source, dest,   "_RimFresnelPower","_RimPower");
        TryCopyFloat(source, dest,   "_Cutoff",         "_Cutoff");

        SetTransparentBlend(dest);
        return dest;
    }

    private Material RemapStandardToURPLit(Material source)
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return null;

        var dest = new Material(urpLit) { name = source.name + " [URP]" };

        TryCopyColor(source, dest,   "_Color",    "_BaseColor");
        TryCopyTexture(source, dest, "_MainTex",  "_BaseMap");
        TryCopyColor(source, dest,   "_EmissionColor", "_EmissionColor");
        TryCopyTexture(source, dest, "_EmissionMap",   "_EmissionMap");
        TryCopyFloat(source, dest,   "_Metallic", "_Metallic");
        TryCopyFloat(source, dest,   "_Glossiness","_Smoothness");

        return dest;
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private void BackupMaterials()
    {
        _originalMaterials.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            // Deep copy the array
            var copy = new Material[r.sharedMaterials.Length];
            r.sharedMaterials.CopyTo(copy, 0);
            _originalMaterials[r] = copy;
        }
    }

    private void ApplyMaterial(Renderer renderer, int index, Material mat)
    {
        var mats = renderer.sharedMaterials;
        if (index < mats.Length)
        {
            mats[index] = mat;
            renderer.sharedMaterials = mats;
        }
    }

    private static void TryCopyColor(Material s, Material d, string sp, string dp)
    {
        if (s.HasProperty(sp) && d.HasProperty(dp)) d.SetColor(dp, s.GetColor(sp));
    }

    private static void TryCopyTexture(Material s, Material d, string sp, string dp)
    {
        if (s.HasProperty(sp) && d.HasProperty(dp))
        {
            d.SetTexture(dp, s.GetTexture(sp));
            d.SetTextureOffset(dp, s.GetTextureOffset(sp));
            d.SetTextureScale(dp, s.GetTextureScale(sp));
        }
    }

    private static void TryCopyFloat(Material s, Material d, string sp, string dp)
    {
        if (s.HasProperty(sp) && d.HasProperty(dp)) d.SetFloat(dp, s.GetFloat(sp));
    }

    private static void SetTransparentBlend(Material mat)
    {
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite",   0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
