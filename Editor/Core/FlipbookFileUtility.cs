using System.IO;

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
    }
}
