using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal static class FlipbookMaterialBuilder
    {
        private const string ShaderName = "Sebanne/FlipbookShader";
        private const string ArrayShaderName = "Sebanne/FlipbookArrayShader";
        private const string SequenceShaderName = "Sebanne/FlipbookSequenceShader";
        private const string LilToonShaderName = "lilToon";

        internal static bool IsLilToonAvailable() => Shader.Find(LilToonShaderName) != null;

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

            var existing = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            if (existing != null)
            {
                existing.shader = material.shader;
                existing.CopyPropertiesFromMaterial(material);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();

                FlipbookGeneratorLog.Info(
                    $"Material updated: {outputPath} ({sheetResult.Columns}x{sheetResult.Rows}, " +
                    $"{sheetResult.TotalFrames} frames, {fps} FPS)");
                return existing;
            }
            AssetDatabase.CreateAsset(material, outputPath);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

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

            var existing = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            if (existing != null)
            {
                existing.shader = material.shader;
                existing.CopyPropertiesFromMaterial(material);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();

                FlipbookGeneratorLog.Info(
                    $"Material updated: {outputPath} (Texture2DArray, " +
                    $"{arrayResult.TotalFrames} frames, {fps} FPS)");
                return existing;
            }
            AssetDatabase.CreateAsset(material, outputPath);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            FlipbookGeneratorLog.Info(
                $"Material saved: {outputPath} (Texture2DArray, " +
                $"{arrayResult.TotalFrames} frames, {fps} FPS)");

            return material;
        }

        internal static Material BuildForSequence(
            FlipbookSheetResult sheetResult,
            Texture2D sheet,
            string outputPath)
        {
            var shader = Shader.Find(SequenceShaderName);
            if (shader == null)
            {
                FlipbookGeneratorLog.Error($"Shader not found: {SequenceShaderName}");
                return null;
            }

            var material = new Material(shader);
            material.SetTexture("_MainTex", sheet);
            material.SetInt("_Columns", sheetResult.Columns);
            material.SetInt("_Rows", sheetResult.Rows);
            material.SetInt("_TotalFrames", sheetResult.TotalFrames);
            material.SetFloat(FlipbookConstants.ShaderCurrentFrame, 0f);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolderRecursive(directory);
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            if (existing != null)
            {
                existing.shader = material.shader;
                existing.CopyPropertiesFromMaterial(material);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();

                FlipbookGeneratorLog.Info(
                    $"Material updated (Sequence): {outputPath} ({sheetResult.Columns}x{sheetResult.Rows}, " +
                    $"{sheetResult.TotalFrames} frames)");
                return existing;
            }
            AssetDatabase.CreateAsset(material, outputPath);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            FlipbookGeneratorLog.Info(
                $"Material saved (Sequence): {outputPath} ({sheetResult.Columns}x{sheetResult.Rows}, " +
                $"{sheetResult.TotalFrames} frames)");

            return material;
        }

        internal static Material BuildForLilToon(
            Texture2D spriteSheet,
            int columns,
            int rows,
            int totalFrames,
            string outputPath,
            float fps = 12f)
        {
            var shader = Shader.Find(LilToonShaderName);
            if (shader == null)
            {
                FlipbookGeneratorLog.Error($"Shader not found: {LilToonShaderName}. Is lilToon installed?");
                return null;
            }

            var material = new Material(shader);

            // Enable Main2nd texture and configure as decal flipbook
            material.SetFloat("_UseMain2ndTex", 1f);
            material.SetTexture("_Main2ndTex", spriteSheet);
            material.SetFloat("_Main2ndTexIsDecal", 1f);
            material.SetVector("_Main2ndTexDecalAnimation",
                new Vector4(columns, rows, totalFrames, fps));
            material.SetVector("_Main2ndTexDecalSubParam",
                new Vector4(1f, 1f, 0f, 0f)); // loopX, loopY, offset, (unused)

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolderRecursive(directory);
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            if (existing != null)
            {
                existing.shader = material.shader;
                existing.CopyPropertiesFromMaterial(material);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();

                FlipbookGeneratorLog.Info(
                    $"Material updated (lilToon): {outputPath} " +
                    $"({columns}x{rows}, {totalFrames} frames, {fps} FPS)");
                return existing;
            }
            AssetDatabase.CreateAsset(material, outputPath);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            FlipbookGeneratorLog.Info(
                $"Material saved (lilToon): {outputPath} " +
                $"({columns}x{rows}, {totalFrames} frames, {fps} FPS)");

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
