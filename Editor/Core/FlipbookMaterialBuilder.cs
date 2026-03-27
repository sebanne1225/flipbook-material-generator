using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookMaterialBuilder
    {
        private const string ShaderName = "Sebanne/FlipbookShader";
        private const string ArrayShaderName = "Sebanne/FlipbookArrayShader";

        internal static Material Build(
            FlipbookSheetResult sheetResult,
            Texture2D sheet,
            string outputPath,
            float fps = 12f)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                FlipbookGeneratorLog.Error($"Shader not found: {ShaderName}");
                return null;
            }

            var material = new Material(shader);
            material.SetTexture("_MainTex", sheet);
            material.SetInt("_Columns", sheetResult.Columns);
            material.SetInt("_Rows", sheetResult.Rows);
            material.SetInt("_TotalFrames", sheetResult.TotalFrames);
            material.SetFloat("_FPS", fps);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolderRecursive(directory);
            }

            AssetDatabase.CreateAsset(material, outputPath);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            FlipbookGeneratorLog.Info(
                $"Material saved: {outputPath} ({sheetResult.Columns}x{sheetResult.Rows}, " +
                $"{sheetResult.TotalFrames} frames, {fps} FPS)");

            return material;
        }

        internal static Material BuildFromArray(
            FlipbookArrayResult arrayResult,
            Texture2DArray texArray,
            string outputPath,
            float fps = 12f)
        {
            var shader = Shader.Find(ArrayShaderName);
            if (shader == null)
            {
                FlipbookGeneratorLog.Error($"Shader not found: {ArrayShaderName}");
                return null;
            }

            var material = new Material(shader);
            material.SetTexture("_MainTex", texArray);
            material.SetInt("_TotalFrames", arrayResult.TotalFrames);
            material.SetFloat("_FPS", fps);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolderRecursive(directory);
            }

            AssetDatabase.CreateAsset(material, outputPath);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            FlipbookGeneratorLog.Info(
                $"Material saved: {outputPath} (Texture2DArray, " +
                $"{arrayResult.TotalFrames} frames, {fps} FPS)");

            return material;
        }

        private static void CreateFolderRecursive(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parent = Path.GetDirectoryName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderRecursive(parent);
            }

            var folderName = Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
