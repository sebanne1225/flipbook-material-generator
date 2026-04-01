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
        private const int MaxFrames = 64;

        internal static Texture2D[] Load(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                FlipbookGeneratorLog.Error($"Folder not found: {folderPath}");
                return Array.Empty<Texture2D>();
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

            var textures = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         && !p.Contains("/Generated/"))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (textures.Length == 0)
            {
                FlipbookGeneratorLog.Error($"No PNG files found in: {folderPath}");
                return Array.Empty<Texture2D>();
            }

            if (textures.Length > MaxFrames)
            {
                FlipbookGeneratorLog.Warn(
                    $"Found {textures.Length} PNGs, exceeding the {MaxFrames}-frame limit. " +
                    $"Only the first {MaxFrames} will be used.");
                textures = textures.Take(MaxFrames).ToArray();
            }

            var loaded = new List<Texture2D>(textures.Length);
            for (var i = 0; i < textures.Length; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(textures[i]);
                if (tex == null)
                {
                    FlipbookGeneratorLog.Warn($"Skipping unreadable frame: {textures[i]}");
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
        /// Loads all PNG frames without the 64-frame cap. For MultiPageSequence mode.
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
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
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
