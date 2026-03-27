using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    public sealed class FlipbookMaterialGeneratorWindow : EditorWindow
    {
        private const string WindowTitle = "Flipbook Material Generator";

        private DefaultAsset _inputFolder;
        private DefaultAsset _outputFolder;
        private float _fps = 12f;

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

            // FPS
            _fps = EditorGUILayout.FloatField("FPS", _fps);
            if (_fps < 0.1f) _fps = 0.1f;

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

        private void RunDryRun(string inputPath)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { inputPath });
            var count = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            if (count == 0)
            {
                FlipbookGeneratorLog.Error($"[Dry Run] No PNG files found in: {inputPath}");
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
                $"[Dry Run] Frames: {count}" +
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
            var sheetPath = $"{outputDir}/{baseName}_Sheet.png";
            var matPath = $"{outputDir}/{baseName}_Flipbook.mat";

            // 3. Build sprite sheet
            var sheetResult = FlipbookSheetBuilder.Build(frames, sheetPath);
            if (sheetResult == null) return;

            // 4. Load saved sheet texture
            var sheetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetResult.SavedPath);
            if (sheetTexture == null)
            {
                FlipbookGeneratorLog.Error($"Failed to load generated sheet: {sheetResult.SavedPath}");
                return;
            }

            // 5. Build material
            var material = FlipbookMaterialBuilder.Build(sheetResult, sheetTexture, matPath, _fps);
            if (material == null) return;

            FlipbookGeneratorLog.Info(
                $"Generation complete: {sheetResult.TotalFrames} frames -> {sheetPath}, {matPath}");

            // Ping the generated material in Project window
            EditorGUIUtility.PingObject(material);
        }

        private static string AssetPathOrNull(DefaultAsset asset)
        {
            if (asset == null) return null;
            return AssetDatabase.GetAssetPath(asset);
        }
    }
}
