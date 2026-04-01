// AuraVT — VRCPrefabStripper
// Editor-only utility. Strips VRChat SDK component references from Unity
// serialized YAML files (.prefab, .unity, .asset) so they compile and
// load correctly without the VRChat SDK installed.
//
// VRC components stripped:
//   VRC_AvatarDescriptor, VRCAvatarDescriptor
//   VRC_AvatarV3Body, VRCAvatarParametersDriver
//   VRC_PhysBone, VRC_PhysBoneCollider
//   VRC_ContactReceiver, VRC_ContactSender
//   PipelineManager
//   (any MonoBehaviour whose script GUID matches known VRC SDK GUIDs)
//
// Method: Text-based YAML manipulation (Unity serialized format).
// We remove entire MonoBehaviour blocks that reference VRC scripts.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class VRCPrefabStripper
{
    // ── Known VRC component class names (all variants) ────────────────────────
    private static readonly HashSet<string> VRC_COMPONENT_NAMES = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "VRC_AvatarDescriptor",
        "VRCAvatarDescriptor",
        "VRC_AvatarV3Body",
        "VRC_AvatarV3Hands",
        "VRCAvatarParametersDriver",
        "VRC_PhysBone",
        "VRCPhysBone",
        "VRC_PhysBoneCollider",
        "VRCPhysBoneCollider",
        "VRC_ContactReceiver",
        "VRCContactReceiver",
        "VRC_ContactSender",
        "VRCContactSender",
        "PipelineManager",
        "VRC_SpatialAudioSource",
        "VRCSpatialAudioSource",
        "VRC_Station",
        "VRCStation",
        "VRC_IKFollower",
        "VRC_AnimatorLayerControl",
        "VRC_PlayableLayerControl",
    };

    // ── Known VRC SDK script GUIDs (from VRCSDK3-AVATAR package) ─────────────
    // These are stable across SDK versions for the core avatar components.
    private static readonly HashSet<string> VRC_SCRIPT_GUIDS = new HashSet<string>
    {
        "f5b0e1a8b3c2d4e5f6a7b8c9d0e1f2a3",  // VRCAvatarDescriptor placeholder
        "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6",  // VRC_PhysBone placeholder
        // Real GUIDs are extracted at import time via ScanForVRCGUIDs()
    };

    // ── Strip result ──────────────────────────────────────────────────────────
    public struct StripResult
    {
        public int ComponentsRemoved;
        public int FilesModified;
        public List<string> RemovedComponents;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Strip all VRC SDK components from every .prefab file in a directory tree.
    /// Also scans for VRC script GUIDs from any VRCSDK meta files present.
    /// </summary>
    public static StripResult StripDirectory(string rootPath)
    {
        var result = new StripResult
        {
            RemovedComponents = new List<string>()
        };

        if (!Directory.Exists(rootPath)) return result;

        // First pass: collect any VRC script GUIDs from .meta files
        CollectVRCGuids(rootPath);

        // Second pass: strip prefabs and scene files
        foreach (var file in Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".prefab" && ext != ".unity" && ext != ".asset") continue;

            var fileResult = StripFile(file);
            if (fileResult.ComponentsRemoved > 0)
            {
                result.FilesModified++;
                result.ComponentsRemoved += fileResult.ComponentsRemoved;
                result.RemovedComponents.AddRange(fileResult.RemovedComponents);
            }
        }

        Debug.Log($"[AuraVT] VRCPrefabStripper: {result.ComponentsRemoved} VRC components " +
                  $"removed from {result.FilesModified} files.");
        return result;
    }

    /// <summary>
    /// Strip VRC components from a single .prefab / .unity YAML file.
    /// Modifies the file in-place.
    /// </summary>
    public static StripResult StripFile(string filePath)
    {
        var result = new StripResult { RemovedComponents = new List<string>() };

        string text;
        try { text = File.ReadAllText(filePath, Encoding.UTF8); }
        catch { return result; }

        string stripped = StripYAML(text, result);

        if (result.ComponentsRemoved > 0)
        {
            File.WriteAllText(filePath, stripped, Encoding.UTF8);
            Debug.Log($"[AuraVT] Stripped {result.ComponentsRemoved} VRC component(s) from: " +
                      $"{Path.GetFileName(filePath)}");
        }

        return result;
    }

    // ── YAML stripping ────────────────────────────────────────────────────────

    private static string StripYAML(string yaml, StripResult result)
    {
        // Unity serialized YAML uses "--- !u!114 &fileID" to start MonoBehaviour blocks
        // We split on document markers and filter out VRC ones.
        var lines   = yaml.Split('\n');
        var output  = new StringBuilder(yaml.Length);
        var block   = new List<string>();
        bool inVRCBlock = false;
        string currentVRCName = null;

        void FlushBlock()
        {
            if (!inVRCBlock && block.Count > 0)
            {
                foreach (var l in block) output.Append(l).Append('\n');
            }
            else if (inVRCBlock)
            {
                result.ComponentsRemoved++;
                if (currentVRCName != null)
                    result.RemovedComponents.Add(currentVRCName);
            }
            block.Clear();
            inVRCBlock    = false;
            currentVRCName = null;
        }

        foreach (var rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');

            // New YAML document block
            if (line.StartsWith("--- "))
            {
                FlushBlock();
                block.Add(line);
                continue;
            }

            block.Add(line);

            // Detect VRC MonoBehaviour by script name in the block
            if (!inVRCBlock)
            {
                // Check for "m_Name: VRC_..." or "  script: {fileID: 11500000, guid: <vrc_guid>"
                foreach (var vrcName in VRC_COMPONENT_NAMES)
                {
                    if (line.Contains(vrcName))
                    {
                        inVRCBlock    = true;
                        currentVRCName = vrcName;
                        break;
                    }
                }

                // Also check GUID-based detection
                if (!inVRCBlock)
                {
                    var guidMatch = Regex.Match(line, @"guid:\s*([a-f0-9]{32})");
                    if (guidMatch.Success && VRC_SCRIPT_GUIDS.Contains(guidMatch.Groups[1].Value))
                    {
                        inVRCBlock    = true;
                        currentVRCName = "VRC_SDK_Component";
                    }
                }
            }
        }

        FlushBlock();
        return output.ToString();
    }

    // ── GUID collection ───────────────────────────────────────────────────────

    private static void CollectVRCGuids(string rootPath)
    {
        // Look for VRCSDK meta files to extract authoritative GUIDs
        foreach (var metaFile in Directory.GetFiles(rootPath, "*.meta", SearchOption.AllDirectories))
        {
            string name = Path.GetFileNameWithoutExtension(metaFile).ToLowerInvariant();
            if (!name.Contains("vrc") && !name.Contains("vrchat") && !name.Contains("pipeline")) continue;

            try
            {
                string content = File.ReadAllText(metaFile);
                var match = Regex.Match(content, @"guid:\s*([a-f0-9]{32})");
                if (match.Success)
                    VRC_SCRIPT_GUIDS.Add(match.Groups[1].Value);
            }
            catch { }
        }
    }
}
#endif
