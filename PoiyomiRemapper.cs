using UnityEngine;

/// <summary>
/// AuraVT — PoiyomiRemapper
/// 
/// Maps Poiyomi Toon 7.x / 8.x material properties to the AuraVT/AuraToon shader.
/// 
/// Poiyomi property reference (verified against poiyomi-toon source on GitHub):
///   _Color           → base color tint
///   _MainTex         → albedo / base texture
///   _CutoutEnabled + _Cutoff → alpha cutout
///   _OutlineWidth    → outline thickness
///   _OutlineColor    → outline tint
///   _ShadowStrength  → shadow darkness
///   _ShadowColor     → shadow tint
///   _RimLightColor   → rim light color
///   _RimLightWidth   → rim power (inverted — Poi uses width, we use power)
///   _EmissionColor   → emission tint
///   _EmissionMap     → emission texture
/// </summary>
public static class PoiyomiRemapper
{
    private static Shader _auraToonShader;

    private static Shader GetAuraToon()
    {
        if (_auraToonShader == null)
            _auraToonShader = Shader.Find("AuraVT/AuraToon");
        return _auraToonShader;
    }

    /// <summary>
    /// Remap a single Poiyomi material to AuraToon.
    /// Creates a new material instance — does NOT modify the original asset.
    /// </summary>
    public static Material Remap(Material source)
    {
        var toon = GetAuraToon();
        if (toon == null)
        {
            Debug.LogError("[AuraVT] AuraVT/AuraToon shader not found! " +
                           "Make sure AuraToon.shader is in the project.");
            return source;
        }

        var dest = new Material(toon);
        dest.name = source.name + " [AuraVT]";

        // ── Base color / texture ──────────────────────────────────────────────
        CopyColor(source, dest, "_Color",   "_BaseColor");
        CopyTexture(source, dest, "_MainTex", "_BaseMap");

        // ── Alpha cutout ──────────────────────────────────────────────────────
        if (source.HasProperty("_CutoutEnabled") && source.GetFloat("_CutoutEnabled") > 0.5f)
        {
            dest.SetFloat("_AlphaClip", 1f);
            CopyFloat(source, dest, "_Cutoff", "_Cutoff");
        }

        // ── Emission ─────────────────────────────────────────────────────────
        CopyColor(source, dest, "_EmissionColor", "_EmissionColor");
        CopyTexture(source, dest, "_EmissionMap",  "_EmissionMap");

        // ── Shadow / shading ──────────────────────────────────────────────────
        // Poiyomi _ShadowStrength: 0=no shadow, 1=full
        // AuraToon _ShadeStep: threshold where shade begins
        if (source.HasProperty("_ShadowStrength"))
            dest.SetFloat("_ShadeStep", 1f - source.GetFloat("_ShadowStrength") * 0.5f);

        if (source.HasProperty("_ShadowColor"))
        {
            var shadowCol = source.GetColor("_ShadowColor");
            dest.SetColor("_ShadeColor", shadowCol);
        }

        // ── Rim light ─────────────────────────────────────────────────────────
        if (source.HasProperty("_RimLightColor"))
            dest.SetColor("_RimColor", source.GetColor("_RimLightColor"));

        if (source.HasProperty("_RimLightWidth"))
        {
            // Poiyomi: width 0–1 (wider = more rim)
            // AuraToon: power 0.5–8 (lower = more rim)
            float width = source.GetFloat("_RimLightWidth");
            dest.SetFloat("_RimPower", Mathf.Lerp(8f, 0.5f, width));
        }

        // ── Outline ───────────────────────────────────────────────────────────
        if (source.HasProperty("_OutlineWidth"))
            dest.SetFloat("_OutlineWidth", source.GetFloat("_OutlineWidth") * 0.001f);
        if (source.HasProperty("_OutlineColor"))
            dest.SetColor("_OutlineColor", source.GetColor("_OutlineColor"));

        // ── Blend mode ────────────────────────────────────────────────────────
        SetBlendModeTransparent(dest);

        Debug.Log($"[AuraVT] Poiyomi → AuraToon: {source.name}");
        return dest;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void CopyColor(Material src, Material dst, string srcProp, string dstProp)
    {
        if (src.HasProperty(srcProp) && dst.HasProperty(dstProp))
            dst.SetColor(dstProp, src.GetColor(srcProp));
    }

    private static void CopyTexture(Material src, Material dst, string srcProp, string dstProp)
    {
        if (src.HasProperty(srcProp) && dst.HasProperty(dstProp))
        {
            dst.SetTexture(dstProp, src.GetTexture(srcProp));
            dst.SetTextureOffset(dstProp, src.GetTextureOffset(srcProp));
            dst.SetTextureScale(dstProp, src.GetTextureScale(srcProp));
        }
    }

    private static void CopyFloat(Material src, Material dst, string srcProp, string dstProp)
    {
        if (src.HasProperty(srcProp) && dst.HasProperty(dstProp))
            dst.SetFloat(dstProp, src.GetFloat(srcProp));
    }

    private static void SetBlendModeTransparent(Material mat)
    {
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite",   0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
