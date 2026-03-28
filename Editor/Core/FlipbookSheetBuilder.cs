using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal sealed class FlipbookSheetResult
    {
        internal int Columns { get; }
        internal int Rows { get; }
        internal int TotalFrames { get; }
        internal int TextureSize { get; }
        internal string SavedPath { get; }

        internal FlipbookSheetResult(int columns, int rows, int totalFrames, int textureSize, string savedPath)
        {
            Columns = columns;
            Rows = rows;
            TotalFrames = totalFrames;
            TextureSize = textureSize;
            SavedPath = savedPath;
        }
    }

    internal static class FlipbookSheetBuilder
    {
        private const int DefaultFrameSize = 256;

        internal static FlipbookSheetResult Build(Texture2D[] frames, string outputPath, int maxSheetSize)
        {
            if (frames == null || frames.Length == 0)
            {
                FlipbookGeneratorLog.Error("No frames provided to FlipbookSheetBuilder.");
                return null;
            }

            var (columns, rows) = CalculateGrid(frames.Length);
            var frameSize = DefaultFrameSize;
            var sheetWidth = columns * frameSize;
            var sheetHeight = rows * frameSize;

            if (sheetWidth > maxSheetSize || sheetHeight > maxSheetSize)
            {
                frameSize = Math.Min(maxSheetSize / columns, maxSheetSize / rows);
                sheetWidth = columns * frameSize;
                sheetHeight = rows * frameSize;
                FlipbookGeneratorLog.Warn(
                    $"Frame size reduced to {frameSize}px to fit within {maxSheetSize}x{maxSheetSize}.");
            }

            var sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false);
            var clearPixels = new Color32[sheetWidth * sheetHeight];
            sheet.SetPixels32(clearPixels);

            for (var i = 0; i < frames.Length; i++)
            {
                var col = i % columns;
                var row = rows - 1 - i / columns; // top-left origin
                var readable = MakeReadable(frames[i], frameSize, frameSize);
                if (readable == null)
                {
                    FlipbookGeneratorLog.Error($"Failed to read frame {i}: {frames[i].name}");
                    UnityEngine.Object.DestroyImmediate(sheet);
                    return null;
                }

                sheet.SetPixels(col * frameSize, row * frameSize, frameSize, frameSize,
                    readable.GetPixels());
                UnityEngine.Object.DestroyImmediate(readable);
            }

            sheet.Apply();

            var pngBytes = sheet.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sheet);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(outputPath, pngBytes);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            FlipbookGeneratorLog.Info(
                $"Sprite sheet saved: {outputPath} ({columns}x{rows}, {sheetWidth}x{sheetHeight}px)");

            return new FlipbookSheetResult(columns, rows, frames.Length, frameSize, outputPath);
        }

        internal static (int columns, int rows) CalculateGrid(int frameCount)
        {
            if (frameCount <= 0) return (0, 0);
            var columns = Mathf.CeilToInt(Mathf.Sqrt(frameCount));
            var rows = Mathf.CeilToInt((float)frameCount / columns);
            return (columns, rows);
        }

        private static Texture2D MakeReadable(Texture2D source, int width, int height)
        {
            var prev = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }
    }
}
