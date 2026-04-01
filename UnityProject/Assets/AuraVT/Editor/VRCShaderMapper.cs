// AuraVT — VRCShaderMapper
// Editor-only utility. After a VRChat avatar is extracted and stripped,
// this remaps every .mat file in the staging area to use AuraVT/AuraToon
// (or the correct URP equivalent) instead of Poiyomi, lilToon, or other
// VRChat-specific shaders that require the original shader packages.
//
// Works in conjunction with the runtime ShaderDetector/ShaderRemapper
// but operates at Editor-time via AssetDatabase for clean saved assets.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class VRCShaderMapper
{
    private static Shader _auraToon;
    private static Shader _urpLit;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Remap all .mat files in stagingRoot to AuraToon/URP equivalents.
    /// Call AFTER AssetDatabase.Refresh() so materials are imported.
    /// </summary>
    public static int RemapAllMaterials(string stagingRoot)
    {
        _auraToon = Shader.Find("AuraVT/AuraToon");
        _urpLit   = Shader.Find("Universal Render Pipeline/Lit");

        if (_auraToon == null)
        {
            Debug.LogError("[AuraVT] VRCShaderMapper: AuraToon shader not found in project. " +
                           "Make sure AuraToon.shader is imported.");
            return 0;
        }

        int count = 0;
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { stagingRoot });

        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string shaderName = mat.shader != null ? mat.shader.name : "";
            var family = ShaderDetector.Classify(shaderName);

            switch (family)
            {
                case ShaderDetector.ShaderFamily.AuraVT:
                case ShaderDetector.ShaderFamily.URPLit:
                case ShaderDetector.ShaderFamily.URPUnlit:
                    // Already good — skip
                    continue;

                case ShaderDetector.ShaderFamily.Poiyomi:
                    RemapMaterialEditor(mat, path, PoiyomiRemapper.Remap(mat));
                    count++;
                    break;

                case ShaderDetector.ShaderFamily.LilToon:
                    RemapMaterialEditor(mat, path, LilToonRemapper.Remap(mat));
                    count++;
                    break;

                default:
                    // Unknown / MToon / Standard → AuraToon with basic property copy
                    RemapToAuraToonBasic(mat, path);
                    count++;
                    break;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AuraVT] VRCShaderMapper: Remapped {count} material(s) in {stagingRoot}");
        return count;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void RemapMaterialEditor(Material original, string assetPath, Material remapped)
    {
        if (remapped == null || remapped == original) return;

        // Copy remapped properties back onto the original saved asset
        // (we can't replace the asset itself without losing references)
        original.shader = remapped.shader;

        // Transfer all texture properties
        var shader = remapped.shader;
        int propCount = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < propCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);
            var    propType = ShaderUtil.GetPropertyType(shader, i);

            switch (propType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    if (remapped.HasProperty(propName))
                        original.SetColor(propName, remapped.GetColor(propName));
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    if (remapped.HasProperty(propName))
                        original.SetFloat(propName, remapped.GetFloat(propName));
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    if (remapped.HasProperty(propName))
                    {
                        original.SetTexture(propName, remapped.GetTexture(propName));
                        original.SetTextureOffset(propName, remapped.GetTextureOffset(propName));
                        original.SetTextureScale(propName, remapped.GetTextureScale(propName));
                    }
                    break;
            }
        }

        original.renderQueue = remapped.renderQueue;
        EditorUtility.SetDirty(original);
    }

    private static void RemapToAuraToonBasic(Material mat, string assetPath)
    {
        if (_auraToon == null) return;

        // Save old values we care about
        Color baseColor = mat.HasProperty("_Color")   ? mat.GetColor("_Color") : Color.white;
        Texture baseTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;

        mat.shader = _auraToon;

        mat.SetColor("_BaseColor", baseColor);
        if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);

        // Transparent blend defaults
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite",   0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        EditorUtility.SetDirty(mat);
        Debug.Log($"[AuraVT] Basic remap → AuraToon: {Path.GetFileName(assetPath)}");
    }
}
#endif
