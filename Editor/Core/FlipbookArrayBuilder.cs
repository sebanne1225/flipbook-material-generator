using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal sealed class FlipbookArrayResult
    {
        internal int TotalFrames { get; }
        internal int TextureSize { get; }
        internal string SavedPath { get; }

        internal FlipbookArrayResult(int totalFrames, int textureSize, string savedPath)
        {
            TotalFrames = totalFrames;
            TextureSize = textureSize;
            SavedPath = savedPath;
        }
    }

    internal static class FlipbookArrayBuilder
    {
        private const int DefaultFrameSize = 256;

        internal static FlipbookArrayResult Build(Texture2D[] frames, string outputPath)
        {
            if (frames == null || frames.Length == 0)
            {
                FlipbookGeneratorLog.Error("No frames provided to FlipbookArrayBuilder.");
                return null;
            }

            var frameSize = DefaultFrameSize;
            var texArray = new Texture2DArray(
                frameSize, frameSize, frames.Length, TextureFormat.RGBA32, false);

            for (var i = 0; i < frames.Length; i++)
            {
                var readable = MakeReadable(frames[i], frameSize, frameSize);
                if (readable == null)
                {
                    FlipbookGeneratorLog.Error($"Failed to read frame {i}: {frames[i].name}");
                    Object.DestroyImmediate(texArray);
                    return null;
                }

                texArray.SetPixels(readable.GetPixels(), i);
                Object.DestroyImmediate(readable);
            }

            texArray.Apply(false, true);

            var directory = System.IO.Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolderRecursive(directory);
            }

            AssetDatabase.CreateAsset(texArray, outputPath);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            FlipbookGeneratorLog.Info(
                $"Texture2DArray saved: {outputPath} ({frames.Length} slices, {frameSize}x{frameSize}px)");

            return new FlipbookArrayResult(frames.Length, frameSize, outputPath);
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

        private static void CreateFolderRecursive(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parent = System.IO.Path.GetDirectoryName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderRecursive(parent);
            }

            var folderName = System.IO.Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
