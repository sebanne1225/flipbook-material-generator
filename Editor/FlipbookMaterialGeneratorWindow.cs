using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

    internal enum InputMode
    {
        VideoFile,
        PngSequence,
    }

    internal enum PlaybackMode
    {
        Loop,
        ManualReset,
    }

    public sealed class FlipbookMaterialGeneratorWindow : EditorWindow
    {
        private const string WindowTitle = "Flipbook Material Generator";

        private static readonly int[] MaxSheetSizeOptions = { 512, 1024, 2048, 4096 };

        private InputMode _inputMode = InputMode.VideoFile;
        private DefaultAsset _inputFolder;
        private UnityEngine.Object _videoFile;
        private FlipbookVideoConverter.VideoInfo _videoInfo;
        private int _extractMaxResolution = 512;
        private bool _ffmpegAvailable;
        private bool _ffmpegChecked;

        private DefaultAsset _outputFolder;
        private OutputMode _outputMode = OutputMode.SpriteSheet;
        private OutputFolderMode _outputFolderMode = OutputFolderMode.ToolDefault;
        private int _maxSheetSize = 2048;
        private bool _showAdvanced = false;
        private float _fps = 8f;
        private bool _generatePrefab;
        private string _toggleName = "Flipbook";
        private PlaybackMode _playbackMode = PlaybackMode.Loop;

        private enum FlipbookPreset { Recommended, Custom }
        private FlipbookPreset _preset = FlipbookPreset.Recommended;
        private bool _enableMergeAnimator = true;
        private bool _enableObjectToggle = true;
        private bool _enableMenu = true;
        private bool _enableAudioSource;
        private AudioClip _audioClip;

        // MultiPageSequence settings
        private bool _autoSplit = true;
        private int _framesPerPage;

        // Slot system
        private string _outputName = "";
        private int _slotIndex; // 0 = auto (new), 1+ = existing slot
        private string[] _slotList = Array.Empty<string>(); // display names for popup
        private string[] _slotFolderNames = Array.Empty<string>(); // actual folder names

        // FPS helper
        private int _fpsCalcFrameCount;
        private int _fpsCalcMinutes;
        private float _fpsCalcSeconds;

        private void OnEnable()
        {
            if (_framesPerPage <= 0)
                _framesPerPage = FlipbookPageSplitter.CalculateFramesPerPage(_maxSheetSize);
            _ffmpegChecked = false;
            RefreshSlotList();
        }

        private void ApplyPreset(FlipbookPreset preset)
        {
            switch (preset)
            {
                case FlipbookPreset.Recommended:
                    _playbackMode = PlaybackMode.Loop;
                    _enableMergeAnimator = true;
                    _enableObjectToggle = true;
                    _enableMenu = true;
                    _enableAudioSource = false;
                    break;
                case FlipbookPreset.Custom:
                    break;
            }
        }

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
                "動画ファイルまたはPNG連番フォルダからスプライトシートとフリップブックマテリアルを生成します。",
                MessageType.Info);

            EditorGUILayout.Space();

            // Input mode
            var inputModeLabels = new GUIContent[]
            {
                new GUIContent("動画ファイル"),
                new GUIContent("PNG連番フォルダ"),
            };
            _inputMode = (InputMode)EditorGUILayout.Popup(
                new GUIContent("入力モード"),
                (int)_inputMode,
                inputModeLabels);

            if (_inputMode == InputMode.VideoFile)
            {
                // FFmpeg check (once per window open)
                if (!_ffmpegChecked)
                {
                    _ffmpegAvailable = FlipbookVideoConverter.IsFFmpegAvailable();
                    _ffmpegChecked = true;
                }

                EditorGUI.BeginChangeCheck();
                _videoFile = EditorGUILayout.ObjectField("動画ファイル", _videoFile, typeof(UnityEngine.Object), false);
                if (EditorGUI.EndChangeCheck())
                {
                    // Probe on selection change
                    _videoInfo = null;
                    if (_videoFile != null)
                    {
                        var vpath = AssetDatabase.GetAssetPath(_videoFile);
                        if (FlipbookVideoConverter.IsVideoFile(vpath))
                        {
                            _videoInfo = FlipbookVideoConverter.Probe(Path.GetFullPath(vpath));
                            if (string.IsNullOrWhiteSpace(_outputName))
                                _outputName = Path.GetFileNameWithoutExtension(vpath);
                        }
                    }
                }

                if (_videoFile != null && !FlipbookVideoConverter.IsVideoFile(AssetDatabase.GetAssetPath(_videoFile)))
                {
                    EditorGUILayout.HelpBox(
                        "対応形式: mp4, webm, mov, avi, mkv",
                        MessageType.Warning);
                }

                if (!_ffmpegAvailable)
                {
                    EditorGUILayout.HelpBox(
                        "FFmpeg がパスに見つかりません。FFmpeg をインストールし、PATH を通してください。",
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                _inputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                    "Input Folder", _inputFolder, typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck() && _inputFolder != null && string.IsNullOrWhiteSpace(_outputName))
                {
                    var folderPath = AssetDatabase.GetAssetPath(_inputFolder);
                    _outputName = Path.GetFileName(folderPath);
                }
            }

            // Output folder mode
            var folderModeLabels = new GUIContent[]
            {
                new GUIContent("元ソース直下"),
                new GUIContent("ツール共通フォルダ"),
                new GUIContent("フォルダを指定"),
            };
            EditorGUI.BeginChangeCheck();
            _outputFolderMode = (OutputFolderMode)EditorGUILayout.Popup(
                new GUIContent("出力先"),
                (int)_outputFolderMode,
                folderModeLabels);
            if (EditorGUI.EndChangeCheck())
                RefreshSlotList();

            if (_outputFolderMode == OutputFolderMode.Custom)
            {
                EditorGUI.BeginChangeCheck();
                _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                    "Output Folder", _outputFolder, typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck())
                    RefreshSlotList();
            }

            // Output name & slot
            _outputName = EditorGUILayout.TextField("出力名", _outputName);

            using (new EditorGUILayout.HorizontalScope())
            {
                _slotIndex = EditorGUILayout.Popup("スロット", _slotIndex, _slotList);
                if (GUILayout.Button("更新", GUILayout.Width(40)))
                    RefreshSlotList();
            }

            if (_slotIndex > 0 && _slotIndex <= _slotFolderNames.Length)
            {
                EditorGUILayout.HelpBox(
                    $"{_slotFolderNames[_slotIndex - 1]}/ を上書きします",
                    MessageType.Warning);
            }

            // Slot path preview
            if (!string.IsNullOrWhiteSpace(_outputName))
            {
                var sourcePathHint = _inputMode == InputMode.VideoFile && _videoFile != null
                    ? AssetDatabase.GetAssetPath(_videoFile) ?? ""
                    : AssetPathOrNull(_inputFolder) ?? "";
                var previewRoot = ResolveGeneratedRoot(sourcePathHint);
                string previewPath;
                if (_slotIndex > 0 && _slotIndex <= _slotFolderNames.Length)
                    previewPath = $"{previewRoot}/{_slotFolderNames[_slotIndex - 1]}/";
                else
                    previewPath = $"{previewRoot}/{GetNextSlotNumber(previewRoot):D2}_{_outputName}/";
                var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField($"\u2192 {previewPath}", style);
            }

            // Output mode
            _outputMode = (OutputMode)EditorGUILayout.EnumPopup("Output Mode", _outputMode);

            if (_outputMode == OutputMode.LilToon && !FlipbookMaterialBuilder.IsLilToonAvailable())
            {
                EditorGUILayout.HelpBox(
                    "lilToon がプロジェクトに導入されていません。SpriteSheet モードを使用してください。",
                    MessageType.Warning);
            }

            // Advanced settings
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "上級設定");
            if (_showAdvanced)
            {
                if (_inputMode == InputMode.VideoFile)
                {
                    _extractMaxResolution = EditorGUILayout.IntPopup(
                        new GUIContent("最大解像度"),
                        _extractMaxResolution,
                        new[] { new GUIContent("256"), new GUIContent("512"), new GUIContent("1024") },
                        new[] { 256, 512, 1024 });
                }

                var sizeLabels = new GUIContent[]
                {
                    new GUIContent("512"),
                    new GUIContent("1024"),
                    new GUIContent("2048"),
                    new GUIContent("4096"),
                };
                _maxSheetSize = EditorGUILayout.IntPopup(
                    new GUIContent("最大シートサイズ"),
                    _maxSheetSize,
                    sizeLabels,
                    MaxSheetSizeOptions);

                if (_outputMode == OutputMode.MultiPageSequence)
                {
                    _autoSplit = EditorGUILayout.Toggle("自動分割", _autoSplit);
                    if (_autoSplit)
                    {
                        var auto = FlipbookPageSplitter.CalculateFramesPerPage(_maxSheetSize);
                        if (_framesPerPage <= 0) _framesPerPage = auto;
                        _framesPerPage = EditorGUILayout.IntField("1ページ最大フレーム数", _framesPerPage);
                        EditorGUILayout.HelpBox(
                            $"フレームサイズ 256px / シート上限 {_maxSheetSize}px → 自動算出値: {auto}",
                            MessageType.None);
                    }
                    else
                    {
                        _framesPerPage = EditorGUILayout.IntField("1ページ最大フレーム数", _framesPerPage);
                    }
                    if (_framesPerPage < 1) _framesPerPage = 1;
                }
            }

            // MultiPageSequence settings
            if (_outputMode == OutputMode.MultiPageSequence)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("MultiPageSequence 設定", EditorStyles.boldLabel);

                // Split preview
                var frameCountForPreview = 0;
                if (_inputMode == InputMode.VideoFile && _videoInfo != null)
                {
                    frameCountForPreview = Mathf.RoundToInt(_videoInfo.Duration * _fps);
                }
                else
                {
                    var previewPath = AssetPathOrNull(_inputFolder);
                    if (previewPath != null && AssetDatabase.IsValidFolder(previewPath))
                        frameCountForPreview = CountPngFiles(previewPath);
                }

                if (frameCountForPreview > 0)
                {
                    var effectiveFpp = _framesPerPage > 0 ? _framesPerPage : FlipbookPageSplitter.CalculateFramesPerPage(_maxSheetSize);
                    var preview = BuildSplitPreview(frameCountForPreview, effectiveFpp);
                    EditorGUILayout.HelpBox(preview, MessageType.Info);
                }

            }

            // FPS
            if (_inputMode == InputMode.VideoFile)
            {
                _fps = EditorGUILayout.FloatField("書き出し FPS", _fps);
                if (_fps < 0.1f) _fps = 0.1f;

                if (_videoInfo != null)
                {
                    var estimatedFrames = Mathf.RoundToInt(_videoInfo.Duration * _fps);
                    EditorGUILayout.HelpBox(
                        $"元動画: {_videoInfo.Width}x{_videoInfo.Height}, " +
                        $"{_videoInfo.Fps:F1}fps, {_videoInfo.Duration:F1}秒\n" +
                        $"書き出し {_fps:F1}fps → 推定 {estimatedFrames} フレーム",
                        MessageType.Info);
                }
            }
            else
            {
                _fps = EditorGUILayout.FloatField("PNG Sequence FPS", _fps);
                if (_fps < 0.1f) _fps = 0.1f;
                EditorGUILayout.HelpBox(
                    "映像のFPSではなく、PNG書き出し時のFPSを入力してください。\n" +
                    "・PNG枚数と動画秒数を入力するとFPSを自動計算できます\n" +
                    "・「Input Folderから取得」でInput Folder内のPNG枚数を自動入力できます",
                    MessageType.None);

                // FPS helper
                using (new EditorGUILayout.HorizontalScope())
                {
                    _fpsCalcFrameCount = EditorGUILayout.IntField("PNG 枚数", _fpsCalcFrameCount);
                    var calcInputPath = AssetPathOrNull(_inputFolder);
                    if (calcInputPath != null && AssetDatabase.IsValidFolder(calcInputPath))
                    {
                        if (GUILayout.Button("Input Folderから取得", GUILayout.ExpandWidth(false)))
                            _fpsCalcFrameCount = CountPngFiles(calcInputPath);
                    }
                }
                _fpsCalcMinutes = EditorGUILayout.IntField("動画 分", _fpsCalcMinutes);
                _fpsCalcSeconds = EditorGUILayout.FloatField("動画 秒", _fpsCalcSeconds);

                var totalDuration = _fpsCalcMinutes * 60f + _fpsCalcSeconds;
                var calculatedFps = totalDuration > 0f ? _fpsCalcFrameCount / totalDuration : 0f;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(totalDuration > 0f ? $"計算結果: {calculatedFps:F1} fps" : "計算結果: -");
                    using (new EditorGUI.DisabledScope(totalDuration <= 0f))
                    {
                        if (GUILayout.Button("FPS に適用", GUILayout.ExpandWidth(false)))
                            _fps = calculatedFps;
                    }
                }
            }

            // Prefab generation
            _generatePrefab = EditorGUILayout.Toggle("Prefab も生成する", _generatePrefab);

            if (_generatePrefab && _outputMode == OutputMode.MultiPageSequence)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("再生・MA 設定", EditorStyles.boldLabel);

                // Preset toolbar
                var presetLabels = new[] { "おすすめ", "カスタム" };
                EditorGUI.BeginChangeCheck();
                var newPreset = (FlipbookPreset)GUILayout.Toolbar((int)_preset, presetLabels);
                if (EditorGUI.EndChangeCheck() && newPreset != _preset)
                {
                    _preset = newPreset;
                    ApplyPreset(_preset);
                }

                EditorGUI.indentLevel++;

                // Individual settings (always visible)
                EditorGUI.BeginChangeCheck();

                _playbackMode = (PlaybackMode)EditorGUILayout.EnumPopup("再生モード", _playbackMode);

                _enableMergeAnimator = EditorGUILayout.Toggle("MA Merge Animator", _enableMergeAnimator);

                if (_enableMergeAnimator)
                    _enableObjectToggle = EditorGUILayout.Toggle("MA Object Toggle", _enableObjectToggle);

                if (_enableObjectToggle && _enableMergeAnimator)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _toggleName = EditorGUILayout.TextField("トグル名", _toggleName);
                        var toggleFolderPath = AssetPathOrNull(_inputFolder);
                        if (toggleFolderPath != null && AssetDatabase.IsValidFolder(toggleFolderPath))
                        {
                            if (GUILayout.Button("Input Folderから取得", GUILayout.ExpandWidth(false)))
                                _toggleName = Path.GetFileName(toggleFolderPath);
                        }
                    }

                    _enableAudioSource = EditorGUILayout.Toggle("音源を追加", _enableAudioSource);
                    if (_enableAudioSource)
                        _audioClip = (AudioClip)EditorGUILayout.ObjectField("AudioClip", _audioClip, typeof(AudioClip), false);
                }

                if (EditorGUI.EndChangeCheck())
                    _preset = FlipbookPreset.Custom;

                EditorGUI.indentLevel--;
            }
            else if (_generatePrefab)
            {
                // Non-MultiPageSequence modes: simple MA toggles
                EditorGUI.indentLevel++;
                _enableObjectToggle = EditorGUILayout.Toggle("MA Object Toggle を追加", _enableObjectToggle);
                if (_enableObjectToggle)
                {
                    _enableMenu = EditorGUILayout.Toggle("MA Menu を追加", _enableMenu);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _toggleName = EditorGUILayout.TextField("トグル名", _toggleName);
                        var toggleFolderPath = AssetPathOrNull(_inputFolder);
                        if (toggleFolderPath != null && AssetDatabase.IsValidFolder(toggleFolderPath))
                        {
                            if (GUILayout.Button("Input Folderから取得", GUILayout.ExpandWidth(false)))
                                _toggleName = Path.GetFileName(toggleFolderPath);
                        }
                    }

                    _enableAudioSource = EditorGUILayout.Toggle("音源を追加", _enableAudioSource);
                    if (_enableAudioSource)
                        _audioClip = (AudioClip)EditorGUILayout.ObjectField("AudioClip", _audioClip, typeof(AudioClip), false);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            var hasOutputName = !string.IsNullOrWhiteSpace(_outputName);
            bool hasInput;
            if (_inputMode == InputMode.VideoFile)
            {
                var videoAssetPath = _videoFile != null ? AssetDatabase.GetAssetPath(_videoFile) : null;
                hasInput = videoAssetPath != null && FlipbookVideoConverter.IsVideoFile(videoAssetPath) && _ffmpegAvailable;
            }
            else
            {
                var inputPath = AssetPathOrNull(_inputFolder);
                hasInput = inputPath != null && AssetDatabase.IsValidFolder(inputPath);
            }

            using (new EditorGUI.DisabledScope(!hasInput || !hasOutputName))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Dry Run"))
                    {
                        if (_inputMode == InputMode.VideoFile)
                            RunDryRunFromVideo();
                        else
                            RunDryRun(AssetPathOrNull(_inputFolder));
                    }

                    if (GUILayout.Button("Generate"))
                    {
                        if (_inputMode == InputMode.VideoFile)
                            RunGenerateFromVideo();
                        else
                            RunGenerate(AssetPathOrNull(_inputFolder));
                    }
                }
            }

            if (!hasOutputName)
            {
                EditorGUILayout.HelpBox("出力名を入力してください。", MessageType.Warning);
            }
            else if (!hasInput)
            {
                if (_inputMode == InputMode.VideoFile)
                {
                    EditorGUILayout.HelpBox(
                        _ffmpegAvailable
                            ? "動画ファイルを Assets/ 以下に配置して指定してください。"
                            : "FFmpeg をインストールし、PATH を通してから動画ファイルを指定してください。",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Input Folder に PNG 連番が入った Assets/ 以下のフォルダを指定してください。",
                        MessageType.Warning);
                }
            }
        }

        private int CountPngFiles(string inputPath)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { inputPath });
            var count = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/Generated_Flipbook/"))
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

            if (sheetWidth > _maxSheetSize || sheetHeight > _maxSheetSize)
            {
                frameSize = Mathf.Min(_maxSheetSize / columns, _maxSheetSize / rows);
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

            if (sheetWidth > _maxSheetSize || sheetHeight > _maxSheetSize)
            {
                FlipbookGeneratorLog.Warn(
                    $"[Dry Run] Output exceeds {_maxSheetSize}px. Quest compatibility may be affected.");
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

            if (sheetWidth > _maxSheetSize || sheetHeight > _maxSheetSize)
            {
                frameSize = Mathf.Min(_maxSheetSize / columns, _maxSheetSize / rows);
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

            if (sheetWidth > _maxSheetSize || sheetHeight > _maxSheetSize)
            {
                FlipbookGeneratorLog.Warn(
                    $"[Dry Run] Output exceeds {_maxSheetSize}px. Quest compatibility may be affected.");
            }
        }

        private void RunDryRunMultiPage(int count)
        {
            var fpp = _framesPerPage > 0 ? _framesPerPage : FlipbookPageSplitter.CalculateFramesPerPage(_maxSheetSize);
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
            var frames = FlipbookFrameLoader.Load(inputPath);
            if (frames.Length == 0) return;

            var outputDir = ResolveSlotDir(inputPath);
            EnsureFolderExists(outputDir);
            var baseName = _outputName;

            switch (_outputMode)
            {
                case OutputMode.Texture2DArray:
                    GenerateArray(frames, outputDir, baseName);
                    break;
                case OutputMode.LilToon:
                    GenerateLilToon(frames, outputDir, baseName);
                    break;
                case OutputMode.MultiPageSequence:
                    var allFrames = FlipbookFrameLoader.LoadAll(inputPath);
                    if (allFrames.Length == 0) return;
                    GenerateMultiPageSequenceFromFrames(allFrames, outputDir, baseName);
                    break;
                default:
                    GenerateSheet(frames, outputDir, baseName);
                    break;
            }

            RefreshSlotList();
        }

        private void GenerateSheet(Texture2D[] frames, string outputDir, string baseName)
        {
            var sheetPath = $"{outputDir}/{baseName}_Sheet.png";
            var matPath = $"{outputDir}/{baseName}_Flipbook.mat";

            var sheetResult = FlipbookSheetBuilder.Build(frames, sheetPath, _maxSheetSize);
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
                FlipbookPrefabBuilder.Build(material, outputDir, baseName, _enableObjectToggle, _toggleName, _enableMenu, _enableAudioSource, _audioClip);

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
                FlipbookPrefabBuilder.Build(material, outputDir, baseName, _enableObjectToggle, _toggleName, _enableMenu, _enableAudioSource, _audioClip);

            EditorGUIUtility.PingObject(material);
        }

        private void GenerateLilToon(Texture2D[] frames, string outputDir, string baseName)
        {
            var sheetPath = $"{outputDir}/{baseName}_Sheet.png";
            var matPath = $"{outputDir}/{baseName}_LilToon.mat";

            var sheetResult = FlipbookSheetBuilder.Build(frames, sheetPath, _maxSheetSize);
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
                FlipbookPrefabBuilder.Build(material, outputDir, baseName, _enableObjectToggle, _toggleName, _enableMenu, _enableAudioSource, _audioClip);

            EditorGUIUtility.PingObject(material);
        }

        private void GenerateMultiPageSequenceFromFrames(Texture2D[] allFrames, string outputDir, string baseName)
        {
            if (allFrames.Length == 0) return;

            // Split into pages
            var fpp = _framesPerPage > 0 ? _framesPerPage : FlipbookPageSplitter.CalculateFramesPerPage(_maxSheetSize);
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

                var sheetResult = FlipbookSheetBuilder.Build(page.Frames, sheetPath, _maxSheetSize);
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
                    i, splitResult.PageCount, page.Frames.Length, _fps, clipPath);
                if (clip == null) return;
                clips[i] = clip;
            }

            // 6. AnimatorController
            var controllerPath = $"{animDir}/{baseName}_Animator.controller";
            FlipbookGeneratorLog.Info($"AnimatorBuilder: clips.Length = {clips.Length}");
            AnimatorController controller;
            try
            {
                controller = FlipbookAnimatorBuilder.Build(clips, controllerPath, _playbackMode);
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
                FlipbookPrefabBuilder.BuildMultiPage(materials, controller, prefabsDir, baseName, _enableObjectToggle, _toggleName, _enableMergeAnimator, _enableMenu, _enableAudioSource, _audioClip, _playbackMode);

            EditorGUIUtility.PingObject(controller);
        }

        private void RunDryRunFromVideo()
        {
            if (_videoInfo == null)
            {
                FlipbookGeneratorLog.Error("[Dry Run] 動画情報を取得できません。動画ファイルを再指定してください。");
                return;
            }

            var estimatedFrames = Mathf.RoundToInt(_videoInfo.Duration * _fps);
            if (estimatedFrames <= 0)
            {
                FlipbookGeneratorLog.Error("[Dry Run] 推定フレーム数が 0 です。FPS または動画を確認してください。");
                return;
            }

            FlipbookGeneratorLog.Info(
                $"[Dry Run] 入力: 動画 ({_videoInfo.Width}x{_videoInfo.Height}, {_videoInfo.Duration:F1}秒), " +
                $"書き出し {_fps:F1}fps, 最大解像度 {_extractMaxResolution}px → 推定 {estimatedFrames} フレーム");

            switch (_outputMode)
            {
                case OutputMode.Texture2DArray:
                    RunDryRunArray(estimatedFrames);
                    break;
                case OutputMode.LilToon:
                    RunDryRunLilToon(estimatedFrames);
                    break;
                case OutputMode.MultiPageSequence:
                    RunDryRunMultiPage(estimatedFrames);
                    break;
                default:
                    RunDryRunSheet(estimatedFrames);
                    break;
            }
        }

        private void RunGenerateFromVideo()
        {
            var assetPath = AssetDatabase.GetAssetPath(_videoFile);
            var fullPath = Path.GetFullPath(assetPath);

            var frames = FlipbookVideoConverter.ExtractFrames(fullPath, _fps, _extractMaxResolution);
            if (frames.Length == 0) return;

            try
            {
                var outputDir = ResolveSlotDirForVideo(assetPath);
                EnsureFolderExists(outputDir);
                var baseName = _outputName;

                switch (_outputMode)
                {
                    case OutputMode.Texture2DArray:
                        GenerateArray(frames, outputDir, baseName);
                        break;
                    case OutputMode.LilToon:
                        GenerateLilToon(frames, outputDir, baseName);
                        break;
                    case OutputMode.MultiPageSequence:
                        GenerateMultiPageSequenceFromFrames(frames, outputDir, baseName);
                        break;
                    default:
                        GenerateSheet(frames, outputDir, baseName);
                        break;
                }

                RefreshSlotList();
            }
            finally
            {
                foreach (var frame in frames)
                    if (frame != null) UnityEngine.Object.DestroyImmediate(frame);
            }
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

        private const string GeneratedFolderName = "Generated_Flipbook";
        private const string ToolDefaultRoot = "Assets/Sebanne/FlipbookMaterialGenerator/" + GeneratedFolderName;
        private static readonly Regex SlotPattern = new Regex(@"^(\d{2})_.+$");

        private string ResolveGeneratedRoot(string sourceAssetPath)
        {
            switch (_outputFolderMode)
            {
                case OutputFolderMode.SourceRelative:
                    if (AssetDatabase.IsValidFolder(sourceAssetPath))
                        return sourceAssetPath + "/" + GeneratedFolderName;
                    var parentDir = Path.GetDirectoryName(sourceAssetPath)?.Replace('\\', '/');
                    return parentDir + "/" + GeneratedFolderName;
                case OutputFolderMode.Custom:
                    var custom = AssetPathOrNull(_outputFolder);
                    if (!string.IsNullOrEmpty(custom)) return custom + "/" + GeneratedFolderName;
                    FlipbookGeneratorLog.Info("Output folder not specified. Falling back to tool default.");
                    return ToolDefaultRoot;
                default:
                    return ToolDefaultRoot;
            }
        }

        private string ResolveSlotDir(string inputAssetPath)
        {
            var root = ResolveGeneratedRoot(inputAssetPath);
            return BuildSlotPath(root);
        }

        private string ResolveSlotDirForVideo(string videoAssetPath)
        {
            var root = ResolveGeneratedRoot(videoAssetPath);
            return BuildSlotPath(root);
        }

        private string BuildSlotPath(string generatedRoot)
        {
            EnsureFolderExists(generatedRoot);

            if (_slotIndex > 0 && _slotIndex <= _slotFolderNames.Length)
            {
                // Existing slot
                return $"{generatedRoot}/{_slotFolderNames[_slotIndex - 1]}";
            }

            // Auto: next number
            var number = GetNextSlotNumber(generatedRoot);
            var folderName = $"{number:D2}_{_outputName}";
            return $"{generatedRoot}/{folderName}";
        }

        private int GetNextSlotNumber(string generatedRoot)
        {
            if (!AssetDatabase.IsValidFolder(generatedRoot)) return 1;

            var maxNumber = 0;
            var subFolders = AssetDatabase.GetSubFolders(generatedRoot);
            foreach (var sub in subFolders)
            {
                var name = Path.GetFileName(sub);
                var match = SlotPattern.Match(name);
                if (match.Success)
                {
                    var num = int.Parse(match.Groups[1].Value);
                    if (num > maxNumber) maxNumber = num;
                }
            }
            return maxNumber + 1;
        }

        private void RefreshSlotList()
        {
            var root = ResolveGeneratedRoot(
                _inputMode == InputMode.VideoFile && _videoFile != null
                    ? AssetDatabase.GetAssetPath(_videoFile) ?? ""
                    : AssetPathOrNull(_inputFolder) ?? "");

            var folders = new System.Collections.Generic.List<string>();
            var names = new System.Collections.Generic.List<string>();

            if (AssetDatabase.IsValidFolder(root))
            {
                foreach (var sub in AssetDatabase.GetSubFolders(root))
                {
                    var name = Path.GetFileName(sub);
                    if (SlotPattern.IsMatch(name))
                    {
                        folders.Add(name);
                        names.Add(name);
                    }
                }
            }

            var displayList = new System.Collections.Generic.List<string> { "自動（新規作成）" };
            displayList.AddRange(names);
            _slotList = displayList.ToArray();
            _slotFolderNames = folders.ToArray();

            // Reset index if out of range
            if (_slotIndex >= _slotList.Length) _slotIndex = 0;
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
