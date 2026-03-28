using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal enum OutputMode
    {
        SpriteSheet,
        Texture2DArray,
        LilToon,
        MultiPageSequence,
    }

    internal enum OutputFolderMode
    {
        SourceRelative,
        ToolDefault,
        Custom,
    }

    public sealed class FlipbookMaterialGeneratorWindow : EditorWindow
    {
        private const string WindowTitle = "Flipbook Material Generator";

        private DefaultAsset _inputFolder;
        private DefaultAsset _outputFolder;
        private OutputMode _outputMode = OutputMode.SpriteSheet;
        private OutputFolderMode _outputFolderMode = OutputFolderMode.ToolDefault;
        private float _fps = 12f;
        private bool _generatePrefab;

        // MultiPageSequence settings
        private bool _autoSplit = true;
        private int _framesPerPage;
        private int _materialIndex;

        [MenuItem("Tools/Sebanne/Flipbook Material Generator")]
        private static void Open()
        {
            var window = GetWindow<FlipbookMaterialGeneratorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(420f, 320f);
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

            // Output folder mode
            var folderModeLabels = new GUIContent[]
            {
                new GUIContent("元ソース直下"),
                new GUIContent("ツール共通フォルダ"),
                new GUIContent("フォルダを指定"),
            };
            _outputFolderMode = (OutputFolderMode)EditorGUILayout.Popup(
                new GUIContent("出力先"),
                (int)_outputFolderMode,
                folderModeLabels);

            if (_outputFolderMode == OutputFolderMode.Custom)
            {
                _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                    "Output Folder", _outputFolder, typeof(DefaultAsset), false);
            }

            // Output mode
            _outputMode = (OutputMode)EditorGUILayout.EnumPopup("Output Mode", _outputMode);

            if (_outputMode == OutputMode.LilToon && !FlipbookMaterialBuilder.IsLilToonAvailable())
            {
                EditorGUILayout.HelpBox(
                    "lilToon がプロジェクトに導入されていません。SpriteSheet モードを使用してください。",
                    MessageType.Warning);
            }

            // MultiPageSequence settings
            if (_outputMode == OutputMode.MultiPageSequence)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("MultiPageSequence 設定", EditorStyles.boldLabel);

                _autoSplit = EditorGUILayout.Toggle("自動分割", _autoSplit);
                if (_autoSplit)
                {
                    var auto = FlipbookPageSplitter.CalculateFramesPerPage();
                    if (_framesPerPage <= 0) _framesPerPage = auto;
                    _framesPerPage = EditorGUILayout.IntField("1ページ最大フレーム数", _framesPerPage);
                    EditorGUILayout.HelpBox(
                        $"フレームサイズ 256px / シート上限 2048px → 自動算出値: {auto}",
                        MessageType.None);
                }
                else
                {
                    _framesPerPage = EditorGUILayout.IntField("1ページ最大フレーム数", _framesPerPage);
                }
                if (_framesPerPage < 1) _framesPerPage = 1;

                // Split preview
                var previewPath = AssetPathOrNull(_inputFolder);
                if (previewPath != null && AssetDatabase.IsValidFolder(previewPath))
                {
                    var pngCount = CountPngFiles(previewPath);
                    if (pngCount > 0)
                    {
                        var preview = BuildSplitPreview(pngCount, _framesPerPage);
                        EditorGUILayout.HelpBox(preview, MessageType.Info);
                    }
                }

                _materialIndex = EditorGUILayout.IntField("Material Index", _materialIndex);
                if (_materialIndex < 0) _materialIndex = 0;
            }

            // FPS
            _fps = EditorGUILayout.FloatField("PNG Sequence FPS", _fps);
            if (_fps < 0.1f) _fps = 0.1f;
            EditorGUILayout.HelpBox("映像のFPSではなく、PNG書き出し時のFPSを入力してください", MessageType.None);

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
                case OutputMode.MultiPageSequence:
                    RunDryRunMultiPage(count);
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

        private void RunDryRunMultiPage(int count)
        {
            var fpp = _framesPerPage > 0 ? _framesPerPage : FlipbookPageSplitter.CalculateFramesPerPage();
            var pageCount = Mathf.CeilToInt((float)count / fpp);
            var preview = BuildSplitPreview(count, fpp);

            var (columns, rows) = FlipbookSheetBuilder.CalculateGrid(Mathf.Min(count, fpp));
            var frameSize = 256;
            var sheetWidth = columns * frameSize;
            var sheetHeight = rows * frameSize;

            FlipbookGeneratorLog.Info(
                $"[Dry Run] Mode: MultiPageSequence, Total frames: {count}, " +
                $"Pages: {preview}, Sheet per page: {sheetWidth}x{sheetHeight}px ({columns}x{rows}), FPS: {_fps}");

            FlipbookGeneratorLog.Info(
                $"[Dry Run] 生成予定: {pageCount} sheets, {pageCount} materials, " +
                $"{pageCount} AnimationClips, 1 AnimatorController" +
                (_generatePrefab ? ", 1 Prefab" : ""));
        }

        private void RunGenerate(string inputPath)
        {
            // 1. Load frames
            var frames = FlipbookFrameLoader.Load(inputPath);
            if (frames.Length == 0) return;

            // 2. Resolve output folder
            var outputDir = ResolveOutputDir(inputPath);
            EnsureFolderExists(outputDir);

            var baseName = Path.GetFileName(inputPath);

            switch (_outputMode)
            {
                case OutputMode.Texture2DArray:
                    GenerateArray(frames, outputDir, baseName);
                    break;
                case OutputMode.LilToon:
                    GenerateLilToon(frames, outputDir, baseName);
                    break;
                case OutputMode.MultiPageSequence:
                    GenerateMultiPageSequence(inputPath, outputDir, baseName);
                    return; // uses LoadAll instead of frames from Load
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

        private void GenerateMultiPageSequence(string inputPath, string outputDir, string baseName)
        {
            // 1. Load all frames (no 64-frame cap)
            var allFrames = FlipbookFrameLoader.LoadAll(inputPath);
            if (allFrames.Length == 0) return;

            // 2. Split into pages
            var fpp = _framesPerPage > 0 ? _framesPerPage : FlipbookPageSplitter.CalculateFramesPerPage();
            var splitResult = FlipbookPageSplitter.Split(allFrames, fpp);
            if (splitResult == null) return;

            // 3. Create subfolders
            var sheetsDir    = $"{outputDir}/Sheets";
            var materialsDir = $"{outputDir}/Materials";
            var animDir      = $"{outputDir}/Animation";
            var prefabsDir   = $"{outputDir}/Prefabs";
            EnsureFolderExists(sheetsDir);
            EnsureFolderExists(materialsDir);
            EnsureFolderExists(animDir);
            EnsureFolderExists(prefabsDir);

            // 4. Per-page: SheetBuilder → MaterialBuilder
            var materials = new Material[splitResult.PageCount];
            for (var i = 0; i < splitResult.PageCount; i++)
            {
                var page = splitResult.Pages[i];
                var sheetPath = $"{sheetsDir}/{baseName}_Page{i + 1}_Sheet.png";
                var matPath   = $"{materialsDir}/{baseName}_Page{i + 1}_Seq.mat";

                var sheetResult = FlipbookSheetBuilder.Build(page.Frames, sheetPath);
                if (sheetResult == null) return;

                var sheetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetResult.SavedPath);
                if (sheetTex == null)
                {
                    FlipbookGeneratorLog.Error($"Failed to load sheet: {sheetResult.SavedPath}");
                    return;
                }

                var mat = FlipbookMaterialBuilder.BuildForSequence(sheetResult, sheetTex, matPath);
                if (mat == null) return;

                materials[i] = mat;
            }

            // 5. Per-page: AnimationClip
            var clips = new AnimationClip[splitResult.PageCount];
            for (var i = 0; i < splitResult.PageCount; i++)
            {
                var page = splitResult.Pages[i];
                var clipPath = $"{animDir}/{baseName}_Page{i + 1}_Anim.anim";
                var clip = FlipbookAnimationBuilder.Build(
                    i, page.Frames.Length, _fps, _materialIndex, clipPath);
                if (clip == null) return;
                clips[i] = clip;
            }

            // 6. AnimatorController
            var controllerPath = $"{animDir}/{baseName}_Animator.controller";
            FlipbookGeneratorLog.Info($"AnimatorBuilder: clips.Length = {clips.Length}");
            AnimatorController controller;
            try
            {
                controller = FlipbookAnimatorBuilder.Build(clips, controllerPath);
            }
            catch (System.Exception e)
            {
                FlipbookGeneratorLog.Error($"AnimatorBuilder threw: {e}");
                return;
            }
            if (controller == null) return;

            FlipbookGeneratorLog.Info(
                $"MultiPageSequence generation complete: {splitResult.TotalFrames} frames -> " +
                $"{splitResult.PageCount} pages, {controllerPath}");

            // 7. Prefab
            if (_generatePrefab)
                FlipbookPrefabBuilder.BuildMultiPage(materials, controller, prefabsDir, baseName);

            EditorGUIUtility.PingObject(controller);
        }

        private static string BuildSplitPreview(int totalFrames, int framesPerPage)
        {
            var pageCount = Mathf.CeilToInt((float)totalFrames / framesPerPage);
            var parts = new string[pageCount];
            for (var i = 0; i < pageCount; i++)
            {
                var start = i * framesPerPage;
                var count = Mathf.Min(framesPerPage, totalFrames - start);
                parts[i] = $"{count}f";
            }
            return $"{pageCount} ページ ({string.Join(" + ", parts)})";
        }

        private string ResolveOutputDir(string inputPath)
        {
            switch (_outputFolderMode)
            {
                case OutputFolderMode.SourceRelative:
                    return inputPath + "/Generated";
                case OutputFolderMode.Custom:
                    var custom = AssetPathOrNull(_outputFolder);
                    if (!string.IsNullOrEmpty(custom)) return custom;
                    FlipbookGeneratorLog.Info("Output folder not specified. Falling back to input folder.");
                    return inputPath;
                default: // ToolDefault
                    return "Assets/Sebanne/FlipbookMaterialGenerator/Generated";
            }
        }

        private static void EnsureFolderExists(string path)
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

        private static string AssetPathOrNull(DefaultAsset asset)
        {
            if (asset == null) return null;
            return AssetDatabase.GetAssetPath(asset);
        }
    }
}
