using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookFrameLoader
    {
        internal static Texture2D[] Load(string folderPath, int maxFrames)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                FlipbookGeneratorLog.Error($"Folder not found: {folderPath}");
                return Array.Empty<Texture2D>();
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

            var texturePaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         && !p.Contains("/Generated_Flipbook/"))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (texturePaths.Length == 0)
            {
                FlipbookGeneratorLog.Error($"No PNG files found in: {folderPath}");
                return Array.Empty<Texture2D>();
            }

            if (texturePaths.Length > maxFrames)
            {
                FlipbookGeneratorLog.Warn(
                    $"Found {texturePaths.Length} PNGs, exceeding the {maxFrames}-frame limit. " +
                    $"Only the first {maxFrames} will be used.");
                texturePaths = texturePaths.Take(maxFrames).ToArray();
            }

            var loaded = new List<Texture2D>(texturePaths.Length);
            for (var i = 0; i < texturePaths.Length; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePaths[i]);
                if (tex == null)
                {
                    FlipbookGeneratorLog.Warn($"Skipping unreadable frame: {texturePaths[i]}");
                    continue;
                }
                loaded.Add(tex);
            }

            if (loaded.Count == 0)
            {
                FlipbookGeneratorLog.Error($"No frames could be loaded from: {folderPath}");
                return Array.Empty<Texture2D>();
            }

            var result = loaded.ToArray();
            FlipbookGeneratorLog.Info($"Loaded {result.Length} frame(s) from: {folderPath}");
            return result;
        }

        /// <summary>
        /// Loads all PNG frames without a frame cap. For MultiPageSequence mode.
        /// </summary>
        internal static Texture2D[] LoadAll(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                FlipbookGeneratorLog.Error($"Folder not found: {folderPath}");
                return Array.Empty<Texture2D>();
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

            var texturePaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         && !p.Contains("/Generated_Flipbook/"))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (texturePaths.Length == 0)
            {
                FlipbookGeneratorLog.Error($"No PNG files found in: {folderPath}");
                return Array.Empty<Texture2D>();
            }

            var loaded = new List<Texture2D>(texturePaths.Length);
            for (var i = 0; i < texturePaths.Length; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePaths[i]);
                if (tex == null)
                {
                    FlipbookGeneratorLog.Warn($"Skipping unreadable frame: {texturePaths[i]}");
                    continue;
                }
                loaded.Add(tex);
            }

            if (loaded.Count == 0)
            {
                FlipbookGeneratorLog.Error($"No frames could be loaded from: {folderPath}");
                return Array.Empty<Texture2D>();
            }

            var result = loaded.ToArray();
            FlipbookGeneratorLog.Info($"Loaded {result.Length} frame(s) from: {folderPath} (no cap)");
            return result;
        }
    }
}
