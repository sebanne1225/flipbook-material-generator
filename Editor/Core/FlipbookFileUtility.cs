using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookFileUtility
    {
        internal static void DeleteFileAndMeta(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (File.Exists(fullPath)) File.Delete(fullPath);
            var metaPath = fullPath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
        }

        internal static void DeleteFolderAndMeta(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
            var metaPath = fullPath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
        }

        internal static void EnsureAssetFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        internal static Texture2D MakeReadable(Texture2D source, int width, int height)
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
