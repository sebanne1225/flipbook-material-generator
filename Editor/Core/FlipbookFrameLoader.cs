using System;
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
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
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

            var result = new Texture2D[textures.Length];
            for (var i = 0; i < textures.Length; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(textures[i]);
                if (tex == null)
                {
                    FlipbookGeneratorLog.Error($"Failed to load texture: {textures[i]}");
                    return Array.Empty<Texture2D>();
                }
                result[i] = tex;
            }

            FlipbookGeneratorLog.Info($"Loaded {result.Length} frame(s) from: {folderPath}");
            return result;
        }
    }
}
