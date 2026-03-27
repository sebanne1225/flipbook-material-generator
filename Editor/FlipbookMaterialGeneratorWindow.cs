using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal enum OutputMode
    {
        SpriteSheet,
        Texture2DArray,
        LilToon,
    }

    public sealed class FlipbookMaterialGeneratorWindow : EditorWindow
    {
        private const string WindowTitle = "Flipbook Material Generator";

        private DefaultAsset _inputFolder;
        private DefaultAsset _outputFolder;
        private OutputMode _outputMode = OutputMode.SpriteSheet;
        private float _fps = 12f;
        private bool _generatePrefab;

        [MenuItem("Tools/Sebanne/Flipbook Material Generator")]
        private static void Open()
        {
            var window = GetWindow<FlipbookMaterialGeneratorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(420f, 260f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "PNG連番フォルダからスプライトシートとフリップブックマテリアルを生成します。",
                MessageType.Info);

            EditorGUILayout.Space();

            // Input folder
            _inputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Input Folder", _inputFolder, typeof(DefaultAsset), false);

            // Output folder
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Output Folder", _outputFolder, typeof(DefaultAsset), false);

            // Output mode
            _outputMode = (OutputMode)EditorGUILayout.EnumPopup("Output Mode", _outputMode);

            if (_outputMode == OutputMode.LilToon && !FlipbookMaterialBuilder.IsLilToonAvailable())
            {
                EditorGUILayout.HelpBox(
                    "lilToon がプロジェクトに導入されていません。SpriteSheet モードを使用してください。",
                    MessageType.Warning);
            }

            // FPS
            _fps = EditorGUILayout.FloatField("FPS", _fps);
            if (_fps < 0.1f) _fps = 0.1f;

            // Prefab generation
            _generatePrefab = EditorGUILayout.Toggle("Prefab も生成する", _generatePrefab);

            EditorGUILayout.Space();

            var inputPath = AssetPathOrNull(_inputFolder);
            var hasInput = inputPath != null && AssetDatabase.IsValidFolder(inputPath);

            using (new EditorGUI.DisabledScope(!hasInput))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Dry Run"))
                    {
                        RunDryRun(inputPath);
                    }

                    if (GUILayout.Button("Generate"))
                    {
                        RunGenerate(inputPath);
                    }
                }
            }

            if (!hasInput)
            {
                EditorGUILayout.HelpBox(
                    "Input Folder に PNG 連番が入った Assets/ 以下のフォルダを指定してください。",
                    MessageType.Warning);
            }
        }

        private int CountPngFiles(string inputPath)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { inputPath });
            var count = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        private void RunDryRun(string inputPath)
        {
            var count = CountPngFiles(inputPath);

            if (count == 0)
            {
                FlipbookGeneratorLog.Error($"[Dry Run] No PNG files found in: {inputPath}");
                return;
            }

            switch (_outputMode)
            {
                case OutputMode.Texture2DArray:
                    RunDryRunArray(count);
                    break;
                case OutputMode.LilToon:
                    RunDryRunLilToon(count);
                    break;
                default:
                    RunDryRunSheet(count);
                    break;
            }
        }

        private void RunDryRunSheet(int count)
        {
            var clamped = count > 64;
            var frameCount = clamped ? 64 : count;
            var (columns, rows) = FlipbookSheetBuilder.CalculateGrid(frameCount);
            var frameSize = 256;
            var sheetWidth = columns * frameSize;
            var sheetHeight = rows * frameSize;

            if (sheetWidth > 2048 || sheetHeight > 2048)
            {
                frameSize = Mathf.Min(2048 / columns, 2048 / rows);
                sheetWidth = columns * frameSize;
                sheetHeight = rows * frameSize;
            }

            FlipbookGeneratorLog.Info(
                $"[Dry Run] Mode: SpriteSheet, Frames: {count}" +
                (clamped ? " (clamped to 64)" : "") +
                $", Grid: {columns}x{rows}" +
                $", Output: {sheetWidth}x{sheetHeight}px" +
                $", Frame size: {frameSize}px");

            if (clamped)
            {
                FlipbookGeneratorLog.Warn(
                    $"[Dry Run] {count} frames exceed the 64-frame limit. Only the first 64 will be used.");
            }

            if (sheetWidth > 2048 || sheetHeight > 2048)
            {
                FlipbookGeneratorLog.Warn(
                    "[Dry Run] Output exceeds 2048px. Quest compatibility may be affected.");
            }
        }

        private void RunDryRunArray(int count)
        {
            var clamped = count > 64;
            var frameCount = clamped ? 64 : count;
            var frameSize = 256;
            var estimatedBytes = (long)frameCount * frameSize * frameSize * 4;
            var estimatedMB = estimatedBytes / (1024f * 1024f);

            FlipbookGeneratorLog.Info(
                $"[Dry Run] Mode: Texture2DArray, Frames: {count}" +
                (clamped ? " (clamped to 64)" : "") +
                $", Frame size: {frameSize}px" +
                $", Estimated: ~{estimatedMB:F1}MB (RGBA32 uncompressed. Actual size may differ after compression)");

            if (clamped)
            {
                FlipbookGeneratorLog.Warn(
                    $"[Dry Run] {count} frames exceed the 64-frame limit. Only the first 64 will be used.");
            }
        }

        private void RunDryRunLilToon(int count)
        {
            if (!FlipbookMaterialBuilder.IsLilToonAvailable())
            {
                FlipbookGeneratorLog.Error("[Dry Run] lilToon is not installed in this project.");
                return;
            }

            var clamped = count > 64;
            var frameCount = clamped ? 64 : count;
            var (columns, rows) = FlipbookSheetBuilder.CalculateGrid(frameCount);
            var frameSize = 256;
            var sheetWidth = columns * frameSize;
            var sheetHeight = rows * frameSize;

            if (sheetWidth > 2048 || sheetHeight > 2048)
            {
                frameSize = Mathf.Min(2048 / columns, 2048 / rows);
                sheetWidth = columns * frameSize;
                sheetHeight = rows * frameSize;
            }

            FlipbookGeneratorLog.Info(
                $"[Dry Run] Mode: LilToon (Main2nd DecalAnimation), Frames: {count}" +
                (clamped ? " (clamped to 64)" : "") +
                $", Grid: {columns}x{rows}" +
                $", Sheet: {sheetWidth}x{sheetHeight}px" +
                $", DecalAnimation: ({columns}, {rows}, {frameCount}, {_fps})");

            if (clamped)
            {
                FlipbookGeneratorLog.Warn(
                    $"[Dry Run] {count} frames exceed the 64-frame limit. Only the first 64 will be used.");
            }

            if (sheetWidth > 2048 || sheetHeight > 2048)
            {
                FlipbookGeneratorLog.Warn(
                    "[Dry Run] Output exceeds 2048px. Quest compatibility may be affected.");
            }
        }

        private void RunGenerate(string inputPath)
        {
            // 1. Load frames
            var frames = FlipbookFrameLoader.Load(inputPath);
            if (frames.Length == 0) return;

            // 2. Resolve output folder
            var outputDir = AssetPathOrNull(_outputFolder);
            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = inputPath;
                FlipbookGeneratorLog.Info($"Output folder not specified. Using input folder: {outputDir}");
            }

            var baseName = Path.GetFileName(inputPath);

            switch (_outputMode)
            {
                case OutputMode.Texture2DArray:
                    GenerateArray(frames, outputDir, baseName);
                    break;
                case OutputMode.LilToon:
                    GenerateLilToon(frames, outputDir, baseName);
                    break;
                default:
                    GenerateSheet(frames, outputDir, baseName);
                    break;
            }
        }

        private void GenerateSheet(Texture2D[] frames, string outputDir, string baseName)
        {
            var sheetPath = $"{outputDir}/{baseName}_Sheet.png";
            var matPath = $"{outputDir}/{baseName}_Flipbook.mat";

            var sheetResult = FlipbookSheetBuilder.Build(frames, sheetPath);
            if (sheetResult == null) return;

            var sheetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetResult.SavedPath);
            if (sheetTexture == null)
            {
                FlipbookGeneratorLog.Error($"Failed to load generated sheet: {sheetResult.SavedPath}");
                return;
            }

            var material = FlipbookMaterialBuilder.Build(sheetResult, sheetTexture, matPath, _fps);
            if (material == null) return;

            FlipbookGeneratorLog.Info(
                $"Generation complete: {sheetResult.TotalFrames} frames -> {sheetPath}, {matPath}");

            if (_generatePrefab)
                FlipbookPrefabBuilder.Build(material, outputDir, baseName);

            EditorGUIUtility.PingObject(material);
        }

        private void GenerateArray(Texture2D[] frames, string outputDir, string baseName)
        {
            var arrayPath = $"{outputDir}/{baseName}_Array.asset";
            var matPath = $"{outputDir}/{baseName}_FlipbookArray.mat";

            var arrayResult = FlipbookArrayBuilder.Build(frames, arrayPath);
            if (arrayResult == null) return;

            var texArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(arrayResult.SavedPath);
            if (texArray == null)
            {
                FlipbookGeneratorLog.Error($"Failed to load generated array: {arrayResult.SavedPath}");
                return;
            }

            var material = FlipbookMaterialBuilder.BuildFromArray(arrayResult, texArray, matPath, _fps);
            if (material == null) return;

            FlipbookGeneratorLog.Info(
                $"Generation complete: {arrayResult.TotalFrames} frames -> {arrayPath}, {matPath}");

            if (_generatePrefab)
                FlipbookPrefabBuilder.Build(material, outputDir, baseName);

            EditorGUIUtility.PingObject(material);
        }

        private void GenerateLilToon(Texture2D[] frames, string outputDir, string baseName)
        {
            var sheetPath = $"{outputDir}/{baseName}_Sheet.png";
            var matPath = $"{outputDir}/{baseName}_LilToon.mat";

            var sheetResult = FlipbookSheetBuilder.Build(frames, sheetPath);
            if (sheetResult == null) return;

            var sheetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetResult.SavedPath);
            if (sheetTexture == null)
            {
                FlipbookGeneratorLog.Error($"Failed to load generated sheet: {sheetResult.SavedPath}");
                return;
            }

            var material = FlipbookMaterialBuilder.BuildForLilToon(
                sheetTexture, sheetResult.Columns, sheetResult.Rows,
                sheetResult.TotalFrames, matPath, _fps);
            if (material == null) return;

            FlipbookGeneratorLog.Info(
                $"Generation complete: {sheetResult.TotalFrames} frames -> {sheetPath}, {matPath}");

            if (_generatePrefab)
                FlipbookPrefabBuilder.Build(material, outputDir, baseName);

            EditorGUIUtility.PingObject(material);
        }

        private static string AssetPathOrNull(DefaultAsset asset)
        {
            if (asset == null) return null;
            return AssetDatabase.GetAssetPath(asset);
        }
    }
}
