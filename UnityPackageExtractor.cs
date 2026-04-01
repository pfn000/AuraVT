// AuraVT — UnityPackageExtractor
// Editor-only utility. Extracts a .unitypackage (gzip-compressed POSIX tar)
// and reconstructs the original Unity project folder structure.
//
// .unitypackage internal layout (per asset):
//   <guid>/
//     asset          ← the binary file (FBX, texture, etc.)
//     asset.meta     ← Unity meta file
//     pathname       ← text file: original relative path e.g. "Assets/MyAvatar/avatar.fbx"
//
// We read every `pathname` file, map it to the `asset` file beside it,
// and copy everything into a staging directory the caller specifies.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class UnityPackageExtractor
{
    // ── Result type ───────────────────────────────────────────────────────────
    public struct ExtractResult
    {
        public bool   Success;
        public string StagingDir;       // Root of extracted files
        public string Error;
        public List<string> FBXPaths;       // All .fbx files found
        public List<string> PrefabPaths;    // All .prefab files found
        public List<string> TexturePaths;   // All texture files found
        public List<string> MaterialPaths;  // All .mat files found
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract a .unitypackage to a staging folder inside the Unity project.
    /// stagingRoot: e.g. "Assets/AuraVT/_VRCImport"
    /// </summary>
    public static ExtractResult Extract(string unityPackagePath, string stagingRoot)
    {
        var result = new ExtractResult
        {
            FBXPaths      = new List<string>(),
            PrefabPaths   = new List<string>(),
            TexturePaths  = new List<string>(),
            MaterialPaths = new List<string>(),
        };

        if (!File.Exists(unityPackagePath))
        {
            result.Error = $"File not found: {unityPackagePath}";
            return result;
        }

        // ── Step 1: Decompress gzip → raw tar bytes in memory ─────────────────
        byte[] tarData;
        try
        {
            using var fs   = File.OpenRead(unityPackagePath);
            using var gz   = new GZipStream(fs, CompressionMode.Decompress);
            using var ms   = new MemoryStream();
            gz.CopyTo(ms);
            tarData = ms.ToArray();
        }
        catch (Exception ex)
        {
            result.Error = $"Decompression failed: {ex.Message}";
            return result;
        }

        // ── Step 2: Parse tar entries → build guid→(asset, pathname) map ──────
        var assetMap = new Dictionary<string, byte[]>();   // guid/filename → bytes
        var pathMap  = new Dictionary<string, string>();   // guid → original pathname

        try
        {
            ParseTar(tarData, (entryName, entryData) =>
            {
                // entryName format: "<guid>/asset", "<guid>/asset.meta", "<guid>/pathname"
                var parts = entryName.TrimStart('/').Split('/');
                if (parts.Length < 2) return;

                string guid     = parts[0];
                string filename = parts[1];

                if (filename == "pathname")
                {
                    // pathname file is plain UTF-8 text with the original asset path
                    pathMap[guid] = Encoding.UTF8.GetString(entryData).Trim();
                }
                else
                {
                    assetMap[$"{guid}/{filename}"] = entryData;
                }
            });
        }
        catch (Exception ex)
        {
            result.Error = $"Tar parse failed: {ex.Message}";
            return result;
        }

        // ── Step 3: Reconstruct folder structure ──────────────────────────────
        string stagingAbsolute = Path.Combine(
            Path.GetDirectoryName(Application.dataPath),  // project root
            stagingRoot.Replace('/', Path.DirectorySeparatorChar)
        );

        Directory.CreateDirectory(stagingAbsolute);

        int filesCopied = 0;

        foreach (var kv in pathMap)
        {
            string guid         = kv.Key;
            string originalPath = kv.Value;  // e.g. "Assets/MyAvatar/mesh.fbx"

            // Reconstruct destination: staging + original path (strip "Assets/" prefix)
            string relative = originalPath.StartsWith("Assets/")
                ? originalPath.Substring("Assets/".Length)
                : originalPath;

            string destPath = Path.Combine(stagingAbsolute, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));

            // Copy the asset binary
            string assetKey = $"{guid}/asset";
            if (assetMap.TryGetValue(assetKey, out var assetBytes))
            {
                File.WriteAllBytes(destPath, assetBytes);
                filesCopied++;
                CategorizeFile(destPath, result);
            }

            // Copy the meta file
            string metaKey = $"{guid}/asset.meta";
            if (assetMap.TryGetValue(metaKey, out var metaBytes))
            {
                File.WriteAllBytes(destPath + ".meta", metaBytes);
            }
        }

        result.StagingDir = stagingRoot;
        result.Success    = filesCopied > 0;

        if (!result.Success)
            result.Error = "No asset files were extracted. Package may be empty or corrupted.";
        else
            Debug.Log($"[AuraVT] Extracted {filesCopied} files to {stagingRoot}");

        return result;
    }

    // ── Tar Parser ────────────────────────────────────────────────────────────
    // Minimal POSIX tar parser — handles ustar format used by Unity.

    private static void ParseTar(byte[] tarData, Action<string, byte[]> onEntry)
    {
        int offset = 0;

        while (offset + 512 <= tarData.Length)
        {
            // Read 512-byte header block
            string name = ReadTarString(tarData, offset, 100);
            if (string.IsNullOrEmpty(name)) break;   // End-of-archive marker

            // File size is stored as octal ASCII in bytes 124–135
            string sizeOctal = ReadTarString(tarData, offset + 124, 12).Trim('\0', ' ');
            long fileSize = sizeOctal.Length > 0 ? Convert.ToInt64(sizeOctal, 8) : 0;

            // UStar prefix (bytes 345–499) prepended to name
            string prefix = ReadTarString(tarData, offset + 345, 155);
            if (!string.IsNullOrEmpty(prefix))
                name = prefix + "/" + name;

            offset += 512;  // Skip header

            if (fileSize > 0 && offset + fileSize <= tarData.Length)
            {
                var data = new byte[fileSize];
                Array.Copy(tarData, offset, data, 0, fileSize);
                onEntry(name, data);
            }

            // Advance to next 512-byte boundary
            offset += (int)(((fileSize + 511) / 512) * 512);
        }
    }

    private static string ReadTarString(byte[] data, int offset, int length)
    {
        int end = offset;
        int max = Math.Min(offset + length, data.Length);
        while (end < max && data[end] != 0) end++;
        return Encoding.UTF8.GetString(data, offset, end - offset);
    }

    // ── File categorization ───────────────────────────────────────────────────

    private static void CategorizeFile(string path, ExtractResult result)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".fbx": case ".obj": case ".dae":
                result.FBXPaths.Add(path);
                break;
            case ".prefab":
                result.PrefabPaths.Add(path);
                break;
            case ".mat":
                result.MaterialPaths.Add(path);
                break;
            case ".png": case ".jpg": case ".jpeg":
            case ".tga": case ".psd": case ".bmp":
                result.TexturePaths.Add(path);
                break;
        }
    }
}
#endif
