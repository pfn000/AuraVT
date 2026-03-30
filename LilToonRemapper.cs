using UnityEngine;

/// <summary>
/// AuraVT — LilToonRemapper
///
/// Maps lilToon 1.7x material properties to the AuraVT/AuraToon shader.
///
/// lilToon property reference (verified against liltoon source on GitHub):
///   _Color             → base color tint
///   _MainTex           → albedo / base texture
///   _Cutoff            → alpha cutoff
///   _ShadowColor       → first shadow color
///   _Shadow2ndColor    → second shadow color (blended into ShadeColor)
///   _ShadowBorderMin/Max → shadow threshold range
///   _RimColor          → rim light color
///   _RimFresnelPower   → rim power
///   _EmissionColor     → emission color
///   _EmissionMap       → emission texture
///   _OutlineColor      → outline color
///   _OutlineWidth      → outline thickness (in cm)
///   _UseShadow         → shadow toggle
///   _UseRim            → rim light toggle
///   _UseEmission       → emission toggle
///   _UseOutline        → outline toggle
/// </summary>
public static class LilToonRemapper
{
    private static Shader _auraToonShader;

    private static Shader GetAuraToon()
    {
        if (_auraToonShader == null)
            _auraToonShader = Shader.Find("AuraVT/AuraToon");
        return _auraToonShader;
    }

    /// <summary>
    /// Remap a single lilToon material to AuraToon.
    /// Creates a new material instance — does NOT modify the original asset.
    /// </summary>
    public static Material Remap(Material source)
    {
        var toon = GetAuraToon();
        if (toon == null)
        {
            Debug.LogError("[AuraVT] AuraVT/AuraToon shader not found!");
            return source;
        }

        var dest = new Material(toon);
        dest.name = source.name + " [AuraVT]";

        // ── Base ──────────────────────────────────────────────────────────────
        CopyColor(source, dest, "_Color",   "_BaseColor");
        CopyTexture(source, dest, "_MainTex", "_BaseMap");
        CopyFloat(source, dest, "_Cutoff",  "_Cutoff");

        // ── Shadow ────────────────────────────────────────────────────────────
        bool useShadow = GetToggle(source, "_UseShadow");
        if (useShadow)
        {
            // lilToon has two shadow layers — blend them for AuraToon's single shade color
            Color shadow1 = source.HasProperty("_ShadowColor")
                ? source.GetColor("_ShadowColor")
                : Color.grey;
            Color shadow2 = source.HasProperty("_Shadow2ndColor")
                ? source.GetColor("_Shadow2ndColor")
                : shadow1;
            dest.SetColor("_ShadeColor", Color.Lerp(shadow1, shadow2, 0.4f));

            // Shadow border: lilToon uses Min/Max, AuraToon uses a single step value
            float borderMin = source.HasProperty("_ShadowBorderMin")
                ? source.GetFloat("_ShadowBorderMin") : 0.4f;
            float borderMax = source.HasProperty("_ShadowBorderMax")
                ? source.GetFloat("_ShadowBorderMax") : 0.6f;
            dest.SetFloat("_ShadeStep", (borderMin + borderMax) * 0.5f);
            dest.SetFloat("_ShadeSoftness", (borderMax - borderMin));
        }
        else
        {
            // No shadow — set shade to base color (flat shading)
            if (source.HasProperty("_Color"))
                dest.SetColor("_ShadeColor", source.GetColor("_Color"));
        }

        // ── Rim light ─────────────────────────────────────────────────────────
        bool useRim = GetToggle(source, "_UseRim");
        if (useRim)
        {
            CopyColor(source, dest, "_RimColor", "_RimColor");
            CopyFloat(source, dest, "_RimFresnelPower", "_RimPower");

            if (source.HasProperty("_RimColorTex"))
                dest.SetTexture("_RimMap", source.GetTexture("_RimColorTex"));
        }
        else
        {
            dest.SetFloat("_RimStrength", 0f);
        }

        // ── Emission ──────────────────────────────────────────────────────────
        bool useEmission = GetToggle(source, "_UseEmission");
        if (useEmission)
        {
            CopyColor(source, dest, "_EmissionColor", "_EmissionColor");
            CopyTexture(source, dest, "_EmissionMap",  "_EmissionMap");
        }

        // ── Outline ───────────────────────────────────────────────────────────
        bool useOutline = GetToggle(source, "_UseOutline");
        if (useOutline)
        {
            CopyColor(source, dest, "_OutlineColor", "_OutlineColor");
            // lilToon outline is in cm; AuraToon expects 0–1 normalized
            if (source.HasProperty("_OutlineWidth"))
                dest.SetFloat("_OutlineWidth", source.GetFloat("_OutlineWidth") * 0.01f);
        }

        // ── Blend mode ────────────────────────────────────────────────────────
        SetBlendModeFromLilToon(source, dest);

        Debug.Log($"[AuraVT] lilToon → AuraToon: {source.name}");
        return dest;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool GetToggle(Material mat, string prop)
        => mat.HasProperty(prop) && mat.GetFloat(prop) > 0.5f;

    private static void CopyColor(Material src, Material dst, string s, string d)
    {
        if (src.HasProperty(s) && dst.HasProperty(d))
            dst.SetColor(d, src.GetColor(s));
    }

    private static void CopyTexture(Material src, Material dst, string s, string d)
    {
        if (src.HasProperty(s) && dst.HasProperty(d))
        {
            dst.SetTexture(d, src.GetTexture(s));
            dst.SetTextureOffset(d, src.GetTextureOffset(s));
            dst.SetTextureScale(d, src.GetTextureScale(s));
        }
    }

    private static void CopyFloat(Material src, Material dst, string s, string d)
    {
        if (src.HasProperty(s) && dst.HasProperty(d))
            dst.SetFloat(d, src.GetFloat(s));
    }

    private static void SetBlendModeFromLilToon(Material src, Material dst)
    {
        // lilToon stores render type in _TransparentMode:
        // 0=Opaque, 1=Cutout, 2=Transparent, 3=FurCutout, 4=Fur
        int transparentMode = src.HasProperty("_TransparentMode")
            ? (int)src.GetFloat("_TransparentMode") : 0;

        switch (transparentMode)
        {
            case 0: // Opaque
                dst.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                dst.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                dst.SetFloat("_ZWrite",   1f);
                dst.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                break;
            case 1: // Cutout
                dst.SetFloat("_AlphaClip", 1f);
                dst.SetFloat("_ZWrite",    1f);
                dst.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                break;
            default: // Transparent
                dst.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dst.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dst.SetFloat("_ZWrite",   0f);
                dst.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;
        }
    }
}
