using System;
using System.Collections.Generic;
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

    public sealed class FlipbookMaterialGeneratorWindow : EditorWindow
    {
        private const string WindowTitle = "Flipbook Material Generator";

        private static readonly int[] MaxSheetSizeOptions = { 512, 1024, 2048, 4096 };

        [SerializeField] private InputMode _inputMode = InputMode.VideoFile;
        [SerializeField] private DefaultAsset _inputFolder;
        [SerializeField] private UnityEngine.Object _videoFile;
        private FlipbookVideoConverter.VideoInfo _videoInfo; // non-serializable, restored via re-probe
        [SerializeField] private int _extractMaxResolution = 512;
        private bool _ffmpegAvailable;  // re-checked on window open
        private bool _ffmpegChecked;    // intentionally reset on domain reload

        [SerializeField] private DefaultAsset _outputFolder;
        [SerializeField] private OutputMode _outputMode = OutputMode.MultiPageSequence;
        [SerializeField] private OutputFolderMode _outputFolderMode = OutputFolderMode.ToolDefault;
        [SerializeField] private int _maxSheetSize = 2048;
        [SerializeField] private bool _showAdvanced = false;
        [SerializeField] private bool _enableConsoleLog;
        [SerializeField] private float _fps = 8f;
        [SerializeField] private bool _generatePrefab;
        [SerializeField] private string _toggleName = "Flipbook";

        private enum FlipbookPreset { Recommended, Custom }
        [SerializeField] private FlipbookPreset _preset = FlipbookPreset.Recommended;
        [SerializeField] private bool _enableMergeAnimator = true;
        [SerializeField] private bool _enableObjectToggle = true;
        [SerializeField] private bool _enableMenu = true;
        [SerializeField] private bool _enableAudioSource;
        [SerializeField] private AudioClip _audioClip;

        // MultiPageSequence settings
        [SerializeField] private bool _autoSplit = true;
        [SerializeField] private int _framesPerPage;

        // Slot system
        [SerializeField] private string _outputName = "";
        [SerializeField] private int _slotIndex; // 0 = auto (new), 1+ = existing slot
        private string[] _slotList = Array.Empty<string>(); // rebuilt by RefreshSlotList
        private string[] _slotFolderNames = Array.Empty<string>(); // rebuilt by RefreshSlotList

        // Slot browser
        [SerializeField] private bool _showSlotBrowser;
        private SlotSummary[] _slotSummaries = Array.Empty<SlotSummary>(); // non-serializable, rebuilt on demand

        private sealed class SlotSummary
        {
            internal string FolderName;
            internal string AssetPath;
            internal int SheetCount;
            internal int MaterialCount;
            internal bool HasPrefab;
            internal bool HasAudio;
        }

        // Trim (Video mode)
        [SerializeField] private bool _enableTrim;
        [SerializeField] private float _trimStart;
        [SerializeField] private float _trimDuration;

        // FPS helper
        [SerializeField] private int _fpsCalcFrameCount;
        [SerializeField] private int _fpsCalcMinutes;
        [SerializeField] private float _fpsCalcSeconds;

        private void OnEnable()
        {
            if (_framesPerPage <= 0)
                _framesPerPage = FlipbookPageSplitter.CalculateFramesPerPage(_maxSheetSize);
            _ffmpegChecked = false;
            RefreshSlotList();
        }

        private void ApplyPreset(FlipbookPreset preset)
        {
            if (preset != FlipbookPreset.Recommended) return;

            if (_outputMode == OutputMode.MultiPageSequence)
            {
                _enableMergeAnimator = true;
                _enableObjectToggle = true;
                _enableMenu = true;
                _enableAudioSource = false;
            }
            else
            {
                _enableObjectToggle = true;
                _enableMenu = true;
                _enableAudioSource = false;
            }
        }

        private static void ShowResultDialog(FlipbookResultInfo info)
        {
            string title;
            if (info.IsDryRun)
                title = "Dry Run 結果";
            else
                title = info.Success ? "Generate 完了" : "Generate 失敗";

            var message = string.Join("\n", info.Lines);
            EditorUtility.DisplayDialog(title, message, "OK");
        }

        private void AppendSettingsLines(FlipbookResultInfo result)
        {
            result.Lines.Add("");
            if (!_generatePrefab) { result.Lines.Add("Prefab: OFF"); return; }

            result.Lines.Add("Prefab: ON");
            if (_outputMode == OutputMode.MultiPageSequence)
                result.Lines.Add($"  MergeAnimator: {OnOff(_enableMergeAnimator)}");
            result.Lines.Add($"  ObjectToggle: {OnOff(_enableObjectToggle)}");
            result.Lines.Add($"  Menu: {OnOff(_enableMenu)}");
            result.Lines.Add($"  AudioSource: {OnOff(_enableAudioSource)}");
        }

        private static string OnOff(bool v) => v ? "ON" : "OFF";

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

                // Re-probe if videoInfo was lost (e.g. window reopen, domain reload)
                if (_videoFile != null && _videoInfo == null)
                {
                    var vpath = AssetDatabase.GetAssetPath(_videoFile);
                    if (FlipbookVideoConverter.IsVideoFile(vpath))
                        _videoInfo = FlipbookVideoConverter.Probe(Path.GetFullPath(vpath));
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
            var modeLabels = new[] { "Texture2DArray", "LilToon", "MultiPageSequence" };
            var modeValues = new[] { OutputMode.Texture2DArray, OutputMode.LilToon, OutputMode.MultiPageSequence };
            var modeIndex = Array.IndexOf(modeValues, _outputMode);
            if (modeIndex < 0) modeIndex = 2; // fallback: MultiPageSequence
            var prevOutputMode = _outputMode;
            modeIndex = EditorGUILayout.Popup("Output Mode", modeIndex, modeLabels);
            _outputMode = modeValues[modeIndex];
            if (_outputMode != prevOutputMode)
            {
                _preset = FlipbookPreset.Recommended;
                ApplyPreset(_preset);
            }

            if (_outputMode == OutputMode.LilToon && !FlipbookMaterialBuilder.IsLilToonAvailable())
            {
                EditorGUILayout.HelpBox(
                    "lilToon がプロジェクトに導入されていません。Texture2DArray モードを使用してください。",
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

                _enableConsoleLog = EditorGUILayout.Toggle("コンソールにログを出力", _enableConsoleLog);
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
                    var effectiveDuration = _enableTrim && _trimDuration > 0f ? _trimDuration : _videoInfo.Duration;
                    frameCountForPreview = Mathf.RoundToInt(effectiveDuration * _fps);
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
                    var fullEstimatedFrames = Mathf.RoundToInt(_videoInfo.Duration * _fps);
                    EditorGUILayout.HelpBox(
                        $"元動画: {_videoInfo.Width}x{_videoInfo.Height}, " +
                        $"{_videoInfo.Fps:F1}fps, {_videoInfo.Duration:F1}秒\n" +
                        $"書き出し {_fps:F1}fps → 推定 {fullEstimatedFrames} フレーム",
                        MessageType.Info);
                }

                // Trim
                _enableTrim = EditorGUILayout.Toggle("トリミング", _enableTrim);
                if (_enableTrim)
                {
                    EditorGUI.indentLevel++;
                    _trimStart = EditorGUILayout.FloatField("開始（秒）", _trimStart);
                    if (_trimStart < 0f) _trimStart = 0f;
                    _trimDuration = EditorGUILayout.FloatField("長さ（秒）", _trimDuration);
                    if (_trimDuration < 0f) _trimDuration = 0f;

                    if (_trimDuration > 0f)
                    {
                        var trimFrames = Mathf.RoundToInt(_trimDuration * _fps);
                        EditorGUILayout.HelpBox(
                            $"トリミング後: {_trimDuration:F1}秒 × {_fps:F1}fps = 推定 {trimFrames} フレーム",
                            MessageType.Info);
                    }

                    if (_videoInfo != null && _trimStart + _trimDuration > _videoInfo.Duration && _trimDuration > 0f)
                    {
                        EditorGUILayout.HelpBox(
                            $"開始+長さ ({_trimStart + _trimDuration:F1}秒) が動画長 ({_videoInfo.Duration:F1}秒) を超えています",
                            MessageType.Warning);
                    }
                    EditorGUI.indentLevel--;
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
                EditorGUILayout.LabelField("Prefab / MA 設定", EditorStyles.boldLabel);

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

                _enableMergeAnimator = EditorGUILayout.Toggle("MA Merge Animator", _enableMergeAnimator);

                if (!_enableMergeAnimator)
                {
                    _enableObjectToggle = false;
                    _enableMenu = false;
                    EditorGUILayout.HelpBox(
                        "MA Merge Animator が OFF の場合、AnimatorController はアバターの FX レイヤーに統合されません。\n" +
                        "VRChat アバターで使う場合は ON にしてください。",
                        MessageType.Warning);
                }

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
                    {
                        _audioClip = (AudioClip)EditorGUILayout.ObjectField("AudioClip", _audioClip, typeof(AudioClip), false);
                        DrawExtractAudioButton();
                    }
                }

                if (EditorGUI.EndChangeCheck())
                    _preset = FlipbookPreset.Custom;

                EditorGUI.indentLevel--;
            }
            else if (_generatePrefab)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Prefab / MA 設定", EditorStyles.boldLabel);

                // Preset toolbar
                var presetLabels3 = new[] { "おすすめ", "カスタム" };
                EditorGUI.BeginChangeCheck();
                var newPreset3 = (FlipbookPreset)GUILayout.Toolbar((int)_preset, presetLabels3);
                if (EditorGUI.EndChangeCheck() && newPreset3 != _preset)
                {
                    _preset = newPreset3;
                    ApplyPreset(_preset);
                }

                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();

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
                    {
                        _audioClip = (AudioClip)EditorGUILayout.ObjectField("AudioClip", _audioClip, typeof(AudioClip), false);
                        DrawExtractAudioButton();
                    }
                }

                if (EditorGUI.EndChangeCheck())
                    _preset = FlipbookPreset.Custom;

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

            // Frame limit check for non-MultiPageSequence modes
            var exceedsFrameLimit = false;
            if (_outputMode != OutputMode.MultiPageSequence && hasInput)
            {
                var maxFrames = FlipbookSheetBuilder.CalculateMaxFrames(_maxSheetSize);
                int estimatedFrameCount;
                if (_inputMode == InputMode.VideoFile && _videoInfo != null)
                {
                    var effectiveDur = _enableTrim && _trimDuration > 0f ? _trimDuration : _videoInfo.Duration;
                    estimatedFrameCount = Mathf.RoundToInt(effectiveDur * _fps);
                }
                else
                {
                    var countPath = AssetPathOrNull(_inputFolder);
                    estimatedFrameCount = countPath != null ? CountPngFiles(countPath) : 0;
                }

                if (estimatedFrameCount > maxFrames)
                {
                    exceedsFrameLimit = true;
                    EditorGUILayout.HelpBox(
                        $"フレーム数（推定 {estimatedFrameCount}）が最大シートサイズの収容上限（{maxFrames} フレーム）を超えています。\n" +
                        "トリミングで減らすか、MultiPageSequence モードを使用してください。",
                        MessageType.Error);
                }
            }

            using (new EditorGUI.DisabledScope(!hasInput || !hasOutputName || exceedsFrameLimit))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Dry Run"))
                    {
                        FlipbookGeneratorLog.Enabled = _enableConsoleLog;
                        FlipbookResultInfo result;
                        if (_inputMode == InputMode.VideoFile)
                            result = RunDryRunFromVideo();
                        else
                            result = RunDryRun(AssetPathOrNull(_inputFolder));
                        if (result != null)
                            ShowResultDialog(result);
                    }

                    if (GUILayout.Button("Generate"))
                    {
                        FlipbookGeneratorLog.Enabled = _enableConsoleLog;
                        FlipbookResultInfo result;
                        if (_inputMode == InputMode.VideoFile)
                            result = RunGenerateFromVideo();
                        else
                            result = RunGenerate(AssetPathOrNull(_inputFolder));
                        if (result != null)
                        {
                            if (!string.IsNullOrEmpty(result.PingAssetPath))
                                EditorGUIUtility.PingObject(
                                    AssetDatabase.LoadMainAssetAtPath(result.PingAssetPath));
                            ShowResultDialog(result);
                        }
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

            // Slot browser
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            _showSlotBrowser = EditorGUILayout.Foldout(_showSlotBrowser, "生成済みスロット一覧");
            if (EditorGUI.EndChangeCheck() && _showSlotBrowser)
                RefreshSlotBrowser();

            if (_showSlotBrowser)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("更新", GUILayout.Width(40)))
                        RefreshSlotBrowser();
                }

                if (_slotSummaries.Length == 0)
                {
                    EditorGUILayout.LabelField("生成済みスロットはありません", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    foreach (var slot in _slotSummaries)
                    {
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                        {
                            if (GUILayout.Button(slot.FolderName, EditorStyles.boldLabel, GUILayout.MinWidth(120)))
                            {
                                RefreshSlotList();
                                for (var i = 0; i < _slotFolderNames.Length; i++)
                                {
                                    if (_slotFolderNames[i] == slot.FolderName)
                                    {
                                        _slotIndex = i + 1;
                                        break;
                                    }
                                }
                            }

                            var summary = $"Sheets:{slot.SheetCount}  Mat:{slot.MaterialCount}  " +
                                          $"Prefab:{(slot.HasPrefab ? "\u25cb" : "\u00d7")}  " +
                                          $"Audio:{(slot.HasAudio ? "\u25cb" : "\u00d7")}";
                            EditorGUILayout.LabelField(summary, EditorStyles.miniLabel, GUILayout.MinWidth(200));

                            if (GUILayout.Button("フォルダを開く", GUILayout.Width(80)))
                            {
                                var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(slot.AssetPath);
                                if (folderAsset != null)
                                    EditorGUIUtility.PingObject(folderAsset);
                            }

                            if (GUILayout.Button("削除", GUILayout.Width(40)))
                            {
                                if (EditorUtility.DisplayDialog(
                                    "スロット削除",
                                    $"{slot.FolderName} を削除しますか？\nAudio を含むすべてのファイルが削除されます。",
                                    "削除", "キャンセル"))
                                {
                                    if (_slotIndex > 0 && _slotIndex <= _slotFolderNames.Length
                                        && _slotFolderNames[_slotIndex - 1] == slot.FolderName)
                                        _slotIndex = 0;
                                    AssetDatabase.DeleteAsset(slot.AssetPath);
                                    RefreshSlotList();
                                    RefreshSlotBrowser();
                                    GUIUtility.ExitGUI();
                                }
                            }
                        }
                    }
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

        private FlipbookResultInfo RunDryRun(string inputPath)
        {
            var count = CountPngFiles(inputPath);

            if (count == 0)
            {
                FlipbookGeneratorLog.Error($"[Dry Run] No PNG files found in: {inputPath}");
                return new FlipbookResultInfo { IsDryRun = true, Success = false, Lines = { "PNG ファイルが見つかりません: " + inputPath } };
            }

            if (_outputMode != OutputMode.MultiPageSequence)
            {
                var maxFrames = FlipbookSheetBuilder.CalculateMaxFrames(_maxSheetSize);
                if (count > maxFrames)
                    FlipbookGeneratorLog.Warn(
                        $"[Dry Run] フレーム数 ({count}) が収容上限 ({maxFrames}) を超えています。" +
                        "MultiPageSequence モードの使用を検討してください。");
            }

            switch (_outputMode)
            {
                case OutputMode.Texture2DArray:
                    return RunDryRunArray(count);
                case OutputMode.LilToon:
                    return RunDryRunLilToon(count);
                case OutputMode.MultiPageSequence:
                    return RunDryRunMultiPage(count);
                default:
                    return null;
            }
        }

        private FlipbookResultInfo RunDryRunArray(int count)
        {
            var result = new FlipbookResultInfo { IsDryRun = true, ModeName = "Texture2DArray" };
            var clamped = count > 64;
            var frameCount = clamped ? 64 : count;
            var frameSize = 256;
            var estimatedBytes = (long)frameCount * frameSize * frameSize * 4;
            var estimatedMB = estimatedBytes / (1024f * 1024f);

            result.Lines.Add($"Mode: Texture2DArray");
            result.Lines.Add($"Frames: {count}" + (clamped ? " (clamped to 64)" : ""));
            result.Lines.Add($"Frame size: {frameSize}px");
            result.Lines.Add($"Estimated: ~{estimatedMB:F1}MB (RGBA32, 未圧縮)");

            FlipbookGeneratorLog.Info(
                $"[Dry Run] Mode: Texture2DArray, Frames: {count}" +
                (clamped ? " (clamped to 64)" : "") +
                $", Frame size: {frameSize}px" +
                $", Estimated: ~{estimatedMB:F1}MB (RGBA32 uncompressed. Actual size may differ after compression)");

            if (clamped)
            {
                result.Lines.Add($"⚠ {count} フレームは上限 64 を超えています。先頭 64 フレームのみ使用されます。");
                FlipbookGeneratorLog.Warn(
                    $"[Dry Run] {count} frames exceed the 64-frame limit. Only the first 64 will be used.");
            }

            result.Lines.Add("");
            result.Lines.Add($"生成予定: 1 Texture2DArray, 1 Material" + (_generatePrefab ? ", 1 Prefab" : ""));
            AppendSettingsLines(result);
            return result;
        }

        private FlipbookResultInfo RunDryRunLilToon(int count)
        {
            var result = new FlipbookResultInfo { IsDryRun = true, ModeName = "LilToon" };

            if (!FlipbookMaterialBuilder.IsLilToonAvailable())
            {
                FlipbookGeneratorLog.Error("[Dry Run] lilToon is not installed in this project.");
                result.Success = false;
                result.Lines.Add("lilToon がプロジェクトに導入されていません。");
                return result;
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

            result.Lines.Add($"Mode: LilToon (Main2nd DecalAnimation)");
            result.Lines.Add($"Frames: {count}" + (clamped ? " (clamped to 64)" : ""));
            result.Lines.Add($"Grid: {columns}x{rows}, Sheet: {sheetWidth}x{sheetHeight}px");
            result.Lines.Add($"DecalAnimation: ({columns}, {rows}, {frameCount}, {_fps})");

            FlipbookGeneratorLog.Info(
                $"[Dry Run] Mode: LilToon (Main2nd DecalAnimation), Frames: {count}" +
                (clamped ? " (clamped to 64)" : "") +
                $", Grid: {columns}x{rows}" +
                $", Sheet: {sheetWidth}x{sheetHeight}px" +
                $", DecalAnimation: ({columns}, {rows}, {frameCount}, {_fps})");

            if (clamped)
            {
                result.Lines.Add($"⚠ {count} フレームは上限 64 を超えています。先頭 64 フレームのみ使用されます。");
                FlipbookGeneratorLog.Warn(
                    $"[Dry Run] {count} frames exceed the 64-frame limit. Only the first 64 will be used.");
            }

            if (sheetWidth > _maxSheetSize || sheetHeight > _maxSheetSize)
            {
                result.Lines.Add($"⚠ 出力が {_maxSheetSize}px を超えています。Quest 互換性に影響する可能性があります。");
                FlipbookGeneratorLog.Warn(
                    $"[Dry Run] Output exceeds {_maxSheetSize}px. Quest compatibility may be affected.");
            }

            result.Lines.Add("");
            result.Lines.Add($"生成予定: 1 SpriteSheet, 1 Material" + (_generatePrefab ? ", 1 Prefab" : ""));
            AppendSettingsLines(result);
            return result;
        }

        private FlipbookResultInfo RunDryRunMultiPage(int count)
        {
            var result = new FlipbookResultInfo { IsDryRun = true, ModeName = "MultiPageSequence" };
            var fpp = _framesPerPage > 0 ? _framesPerPage : FlipbookPageSplitter.CalculateFramesPerPage(_maxSheetSize);
            var pageCount = Mathf.CeilToInt((float)count / fpp);
            var preview = BuildSplitPreview(count, fpp);

            var (columns, rows) = FlipbookSheetBuilder.CalculateGrid(Mathf.Min(count, fpp));
            var frameSize = 256;
            var sheetWidth = columns * frameSize;
            var sheetHeight = rows * frameSize;

            result.Lines.Add($"Mode: MultiPageSequence");
            result.Lines.Add($"Total frames: {count}, FPS: {_fps}");
            result.Lines.Add("");
            result.Lines.Add($"Pages: {preview}");
            result.Lines.Add($"Sheet per page: {sheetWidth}x{sheetHeight}px ({columns}x{rows})");
            result.Lines.Add($"生成予定: {pageCount} sheets, {pageCount} materials, " +
                $"{pageCount} AnimationClips, 1 AnimatorController" +
                (_generatePrefab ? ", 1 Prefab" : ""));

            FlipbookGeneratorLog.Info(
                $"[Dry Run] Mode: MultiPageSequence, Total frames: {count}, " +
                $"Pages: {preview}, Sheet per page: {sheetWidth}x{sheetHeight}px ({columns}x{rows}), FPS: {_fps}");

            FlipbookGeneratorLog.Info(
                $"[Dry Run] 生成予定: {pageCount} sheets, {pageCount} materials, " +
                $"{pageCount} AnimationClips, 1 AnimatorController" +
                (_generatePrefab ? ", 1 Prefab" : ""));

            AppendSettingsLines(result);
            return result;
        }

        private FlipbookResultInfo RunGenerate(string inputPath)
        {
            var frames = FlipbookFrameLoader.Load(inputPath);
            if (frames.Length == 0) return null;

            var outputDir = ResolveSlotDir(inputPath);
            EnsureFolderExists(outputDir);
            if (_slotIndex > 0)
                ClearSlotContents(outputDir);
            var baseName = _outputName;

            FlipbookResultInfo result;
            switch (_outputMode)
            {
                case OutputMode.Texture2DArray:
                    result = GenerateArray(frames, outputDir, baseName);
                    break;
                case OutputMode.LilToon:
                    result = GenerateLilToon(frames, outputDir, baseName);
                    break;
                case OutputMode.MultiPageSequence:
                    var allFrames = FlipbookFrameLoader.LoadAll(inputPath);
                    if (allFrames.Length == 0) return null;
                    result = GenerateMultiPageSequenceFromFrames(allFrames, outputDir, baseName);
                    break;
                default:
                    result = null;
                    break;
            }

            RefreshSlotList();
            return result;
        }

        private FlipbookResultInfo GenerateArray(Texture2D[] frames, string outputDir, string baseName)
        {
            var result = new FlipbookResultInfo { ModeName = "Texture2DArray" };
            var arrayPath = $"{outputDir}/{baseName}_Array.asset";
            var matPath = $"{outputDir}/{baseName}_FlipbookArray.mat";

            var arrayResult = FlipbookArrayBuilder.Build(frames, arrayPath);
            if (arrayResult == null) { result.Success = false; result.Lines.Add("Texture2DArray の生成に失敗しました。"); return result; }

            var texArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(arrayResult.SavedPath);
            if (texArray == null)
            {
                FlipbookGeneratorLog.Error($"Failed to load generated array: {arrayResult.SavedPath}");
                result.Success = false;
                result.Lines.Add($"生成した Texture2DArray の読み込みに失敗しました: {arrayResult.SavedPath}");
                return result;
            }

            var material = FlipbookMaterialBuilder.BuildFromArray(arrayResult, texArray, matPath, _fps);
            if (material == null) { result.Success = false; result.Lines.Add("Material の生成に失敗しました。"); return result; }

            FlipbookGeneratorLog.Info(
                $"Generation complete: {arrayResult.TotalFrames} frames -> {arrayPath}, {matPath}");

            result.Lines.Add($"生成完了: {arrayResult.TotalFrames} フレーム, FPS: {_fps}");
            result.Lines.Add("");
            result.Lines.Add($"Texture2DArray: {Path.GetFileName(arrayPath)}");
            result.Lines.Add($"Material: {Path.GetFileName(matPath)}");

            string prefabPath = null;
            if (_generatePrefab)
                prefabPath = FlipbookPrefabBuilder.Build(material, outputDir, baseName, _enableObjectToggle, _toggleName, _enableMenu, _enableAudioSource, _audioClip);

            if (prefabPath != null)
            {
                result.Lines.Add($"Prefab: {Path.GetFileName(prefabPath)}");
                result.PingAssetPath = prefabPath;
            }
            else
            {
                result.PingAssetPath = matPath;
            }

            AppendSettingsLines(result);
            return result;
        }

        private FlipbookResultInfo GenerateLilToon(Texture2D[] frames, string outputDir, string baseName)
        {
            var result = new FlipbookResultInfo { ModeName = "LilToon" };
            var sheetPath = $"{outputDir}/{baseName}_Sheet.png";
            var matPath = $"{outputDir}/{baseName}_LilToon.mat";

            var sheetResult = FlipbookSheetBuilder.Build(frames, sheetPath, _maxSheetSize);
            if (sheetResult == null) { result.Success = false; result.Lines.Add("スプライトシートの生成に失敗しました。"); return result; }

            var sheetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetResult.SavedPath);
            if (sheetTexture == null)
            {
                FlipbookGeneratorLog.Error($"Failed to load generated sheet: {sheetResult.SavedPath}");
                result.Success = false;
                result.Lines.Add($"生成したスプライトシートの読み込みに失敗しました: {sheetResult.SavedPath}");
                return result;
            }

            var material = FlipbookMaterialBuilder.BuildForLilToon(
                sheetTexture, sheetResult.Columns, sheetResult.Rows,
                sheetResult.TotalFrames, matPath, _fps);
            if (material == null) { result.Success = false; result.Lines.Add("Material の生成に失敗しました。"); return result; }

            FlipbookGeneratorLog.Info(
                $"Generation complete: {sheetResult.TotalFrames} frames -> {sheetPath}, {matPath}");

            result.Lines.Add($"生成完了: {sheetResult.TotalFrames} フレーム, FPS: {_fps}");
            result.Lines.Add($"Grid: {sheetResult.Columns}x{sheetResult.Rows}, Sheet: {sheetResult.Columns * 256}x{sheetResult.Rows * 256}px");
            result.Lines.Add("");
            result.Lines.Add($"SpriteSheet: {Path.GetFileName(sheetPath)}");
            result.Lines.Add($"Material: {Path.GetFileName(matPath)}");

            string prefabPath = null;
            if (_generatePrefab)
                prefabPath = FlipbookPrefabBuilder.Build(material, outputDir, baseName, _enableObjectToggle, _toggleName, _enableMenu, _enableAudioSource, _audioClip);

            if (prefabPath != null)
            {
                result.Lines.Add($"Prefab: {Path.GetFileName(prefabPath)}");
                result.PingAssetPath = prefabPath;
            }
            else
            {
                result.PingAssetPath = matPath;
            }

            AppendSettingsLines(result);
            return result;
        }

        private FlipbookResultInfo GenerateMultiPageSequenceFromFrames(Texture2D[] allFrames, string outputDir, string baseName)
        {
            var result = new FlipbookResultInfo { ModeName = "MultiPageSequence" };
            if (allFrames.Length == 0) { result.Success = false; result.Lines.Add("フレームがありません。"); return result; }

            // Split into pages
            var fpp = _framesPerPage > 0 ? _framesPerPage : FlipbookPageSplitter.CalculateFramesPerPage(_maxSheetSize);
            var splitResult = FlipbookPageSplitter.Split(allFrames, fpp);
            if (splitResult == null) { result.Success = false; result.Lines.Add("ページ分割に失敗しました。"); return result; }

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
                if (sheetResult == null) { result.Success = false; result.Lines.Add($"Page {i + 1} のスプライトシート生成に失敗しました。"); return result; }

                var sheetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetResult.SavedPath);
                if (sheetTex == null)
                {
                    FlipbookGeneratorLog.Error($"Failed to load sheet: {sheetResult.SavedPath}");
                    result.Success = false;
                    result.Lines.Add($"Page {i + 1} のスプライトシート読み込みに失敗しました。");
                    return result;
                }

                var mat = FlipbookMaterialBuilder.BuildForSequence(sheetResult, sheetTex, matPath);
                if (mat == null) { result.Success = false; result.Lines.Add($"Page {i + 1} の Material 生成に失敗しました。"); return result; }

                materials[i] = mat;
            }

            // 5. Per-page: AnimationClip
            var clips = new AnimationClip[splitResult.PageCount];
            for (var i = 0; i < splitResult.PageCount; i++)
            {
                var page = splitResult.Pages[i];
                var clipPath = $"{animDir}/{baseName}_Page{i + 1}_Anim.anim";
                var hasAudio = _enableAudioSource && _audioClip != null;
                var clip = FlipbookAnimationBuilder.Build(
                    i, splitResult.PageCount, page.Frames.Length, _fps, clipPath, hasAudio);
                if (clip == null) { result.Success = false; result.Lines.Add($"Page {i + 1} の AnimationClip 生成に失敗しました。"); return result; }
                clips[i] = clip;
            }

            // 6. Reset clip (Audio restart on manual reset)
            AnimationClip resetClip = null;
            if (_enableAudioSource && _audioClip != null && splitResult.PageCount > 0)
            {
                var resetClipPath = $"{animDir}/{baseName}_ResetPage1_Anim.anim";
                resetClip = FlipbookAnimationBuilder.BuildResetClip(
                    splitResult.PageCount, splitResult.Pages[0].Frames.Length, _fps, resetClipPath);
            }

            // 7. AnimatorController
            var controllerPath = $"{animDir}/{baseName}_Animator.controller";
            AnimatorController controller;
            try
            {
                controller = FlipbookAnimatorBuilder.Build(clips, controllerPath, resetClip);
            }
            catch (System.Exception e)
            {
                FlipbookGeneratorLog.Error($"AnimatorBuilder threw: {e}");
                result.Success = false;
                result.Lines.Add($"AnimatorController の生成中にエラーが発生しました: {e.Message}");
                return result;
            }
            if (controller == null) { result.Success = false; result.Lines.Add("AnimatorController の生成に失敗しました。"); return result; }

            FlipbookGeneratorLog.Info(
                $"MultiPageSequence generation complete: {splitResult.TotalFrames} frames -> " +
                $"{splitResult.PageCount} pages, {controllerPath}");

            var preview = BuildSplitPreview(splitResult.TotalFrames, fpp);
            result.Lines.Add($"生成完了: {splitResult.TotalFrames} フレーム, FPS: {_fps}");
            result.Lines.Add($"Pages: {preview}");
            result.Lines.Add("");
            result.Lines.Add($"AnimatorController: {Path.GetFileName(controllerPath)}");

            // 8. Prefab
            string prefabPath = null;
            if (_generatePrefab)
                prefabPath = FlipbookPrefabBuilder.BuildMultiPage(materials, controller, prefabsDir, baseName, _enableObjectToggle, _toggleName, _enableMergeAnimator, _enableMenu, _enableAudioSource, _audioClip);

            if (prefabPath != null)
            {
                result.Lines.Add($"Prefab: {Path.GetFileName(prefabPath)}");
                result.PingAssetPath = prefabPath;
            }
            else
            {
                result.PingAssetPath = controllerPath;
            }

            AppendSettingsLines(result);
            return result;
        }

        private FlipbookResultInfo RunDryRunFromVideo()
        {
            if (_videoInfo == null)
            {
                FlipbookGeneratorLog.Error("[Dry Run] 動画情報を取得できません。動画ファイルを再指定してください。");
                return new FlipbookResultInfo { IsDryRun = true, Success = false, Lines = { "動画情報を取得できません。動画ファイルを再指定してください。" } };
            }

            var effectiveDuration = _enableTrim && _trimDuration > 0f ? _trimDuration : _videoInfo.Duration;
            var estimatedFrames = Mathf.RoundToInt(effectiveDuration * _fps);
            if (estimatedFrames <= 0)
            {
                FlipbookGeneratorLog.Error("[Dry Run] 推定フレーム数が 0 です。FPS または動画を確認してください。");
                return new FlipbookResultInfo { IsDryRun = true, Success = false, Lines = { "推定フレーム数が 0 です。FPS または動画を確認してください。" } };
            }

            var trimInfo = _enableTrim && _trimDuration > 0f
                ? $", トリミング: {_trimStart:F1}秒〜{_trimStart + _trimDuration:F1}秒"
                : "";
            FlipbookGeneratorLog.Info(
                $"[Dry Run] 入力: 動画 ({_videoInfo.Width}x{_videoInfo.Height}, {_videoInfo.Duration:F1}秒{trimInfo}), " +
                $"書き出し {_fps:F1}fps, 最大解像度 {_extractMaxResolution}px → 推定 {estimatedFrames} フレーム");

            if (_outputMode != OutputMode.MultiPageSequence)
            {
                var maxFrames = FlipbookSheetBuilder.CalculateMaxFrames(_maxSheetSize);
                if (estimatedFrames > maxFrames)
                    FlipbookGeneratorLog.Warn(
                        $"[Dry Run] 推定フレーム数 ({estimatedFrames}) が収容上限 ({maxFrames}) を超えています。" +
                        "MultiPageSequence モードの使用を検討してください。");
            }

            FlipbookResultInfo result;
            switch (_outputMode)
            {
                case OutputMode.Texture2DArray:
                    result = RunDryRunArray(estimatedFrames);
                    break;
                case OutputMode.LilToon:
                    result = RunDryRunLilToon(estimatedFrames);
                    break;
                case OutputMode.MultiPageSequence:
                    result = RunDryRunMultiPage(estimatedFrames);
                    break;
                default:
                    return null;
            }

            // Prepend video input info
            var videoLine = $"入力: 動画 ({_videoInfo.Width}x{_videoInfo.Height}, {_videoInfo.Duration:F1}秒{trimInfo})";
            var extractLine = $"書き出し {_fps:F1}fps, 最大解像度 {_extractMaxResolution}px → 推定 {estimatedFrames} フレーム";
            result.Lines.Insert(0, videoLine);
            result.Lines.Insert(1, extractLine);
            return result;
        }

        private FlipbookResultInfo RunGenerateFromVideo()
        {
            var assetPath = AssetDatabase.GetAssetPath(_videoFile);
            var fullPath = Path.GetFullPath(assetPath);

            var trimStart = _enableTrim ? _trimStart : 0f;
            var trimDuration = _enableTrim ? _trimDuration : 0f;
            var frames = FlipbookVideoConverter.ExtractFrames(fullPath, _fps, _extractMaxResolution, trimStart, trimDuration);
            if (frames.Length == 0) return null;

            try
            {
                var outputDir = ResolveSlotDirForVideo(assetPath);
                EnsureFolderExists(outputDir);
                if (_slotIndex > 0)
                    ClearSlotContents(outputDir);
                var baseName = _outputName;

                FlipbookResultInfo result;
                switch (_outputMode)
                {
                    case OutputMode.Texture2DArray:
                        result = GenerateArray(frames, outputDir, baseName);
                        break;
                    case OutputMode.LilToon:
                        result = GenerateLilToon(frames, outputDir, baseName);
                        break;
                    case OutputMode.MultiPageSequence:
                        result = GenerateMultiPageSequenceFromFrames(frames, outputDir, baseName);
                        break;
                    default:
                        result = null;
                        break;
                }

                RefreshSlotList();
                return result;
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
            var frameCounts = new int[pageCount];
            for (var i = 0; i < pageCount; i++)
                frameCounts[i] = Mathf.Min(framesPerPage, totalFrames - i * framesPerPage);
            return $"{pageCount} ページ ({CompressSplitPreview(frameCounts)})";
        }

        private static string CompressSplitPreview(int[] frameCounts)
        {
            var parts = new List<string>();
            var i = 0;
            while (i < frameCounts.Length)
            {
                var val = frameCounts[i];
                var run = 1;
                while (i + run < frameCounts.Length && frameCounts[i + run] == val) run++;
                parts.Add(run > 1 ? $"{val}f\u00d7{run}" : $"{val}f");
                i += run;
            }
            return string.Join(" + ", parts);
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

        private void RefreshSlotBrowser()
        {
            var root = ResolveGeneratedRoot(
                _inputMode == InputMode.VideoFile && _videoFile != null
                    ? AssetDatabase.GetAssetPath(_videoFile) ?? ""
                    : AssetPathOrNull(_inputFolder) ?? "");

            if (!AssetDatabase.IsValidFolder(root))
            {
                _slotSummaries = Array.Empty<SlotSummary>();
                return;
            }

            var summaries = new System.Collections.Generic.List<SlotSummary>();
            foreach (var sub in AssetDatabase.GetSubFolders(root))
            {
                var folderName = Path.GetFileName(sub);
                if (!SlotPattern.IsMatch(folderName)) continue;

                var sheets = AssetDatabase.FindAssets("t:Texture2D", new[] { sub }).Length;
                var materials = AssetDatabase.FindAssets("t:Material", new[] { sub }).Length;
                var hasPrefab = AssetDatabase.FindAssets("t:GameObject", new[] { sub }).Length > 0;
                var hasAudio = AssetDatabase.FindAssets("t:AudioClip", new[] { sub }).Length > 0;

                summaries.Add(new SlotSummary
                {
                    FolderName = folderName,
                    AssetPath = sub,
                    SheetCount = sheets,
                    MaterialCount = materials,
                    HasPrefab = hasPrefab,
                    HasAudio = hasAudio,
                });
            }

            _slotSummaries = summaries.ToArray();
        }

        private static void ClearSlotContents(string slotDir)
        {
            // Delete subfolders first (preserve Audio/)
            foreach (var sub in AssetDatabase.GetSubFolders(slotDir))
            {
                if (Path.GetFileName(sub) == FlipbookConstants.AudioObjectName) continue;
                AssetDatabase.DeleteAsset(sub);
            }

            // Delete remaining files directly under slot folder (preserve Audio/)
            var guids = AssetDatabase.FindAssets("", new[] { slotDir });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (parent == slotDir)
                {
                    if (Path.GetFileName(path) == FlipbookConstants.AudioObjectName) continue;
                    AssetDatabase.DeleteAsset(path);
                }
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

        private void DrawExtractAudioButton()
        {
            if (_inputMode != InputMode.VideoFile || _videoFile == null || !_ffmpegAvailable)
                return;

            var videoAssetPath = AssetDatabase.GetAssetPath(_videoFile);
            if (!FlipbookVideoConverter.IsVideoFile(videoAssetPath))
                return;

            if (_videoInfo != null && !_videoInfo.HasAudioTrack)
            {
                EditorGUILayout.HelpBox("この動画に音声トラックはありません。", MessageType.Info);
                return;
            }

            if (_enableTrim)
            {
                var trimStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField("トリミング設定が音声にも適用されます", trimStyle);
            }

            if (GUILayout.Button("動画から音声を抽出", GUILayout.ExpandWidth(false)))
            {
                var videoFullPath = Path.GetFullPath(videoAssetPath);

                // Resolve slot and create Audio subfolder
                var slotDir = _inputMode == InputMode.VideoFile
                    ? ResolveSlotDirForVideo(videoAssetPath)
                    : ResolveSlotDir(AssetPathOrNull(_inputFolder) ?? "");
                EnsureFolderExists(slotDir);
                var audioDir = $"{slotDir}/Audio";
                EnsureFolderExists(audioDir);

                var audioTrimStart = _enableTrim ? _trimStart : 0f;
                var audioTrimDuration = _enableTrim ? _trimDuration : 0f;
                var clip = FlipbookVideoConverter.ExtractAudio(videoFullPath, audioDir, _outputName, audioTrimStart, audioTrimDuration);
                if (clip != null)
                {
                    _audioClip = clip;

                    // Switch slot dropdown to the created slot
                    RefreshSlotList();
                    var slotFolderName = Path.GetFileName(slotDir);
                    for (var i = 0; i < _slotFolderNames.Length; i++)
                    {
                        if (_slotFolderNames[i] == slotFolderName)
                        {
                            _slotIndex = i + 1; // 0 = auto, 1+ = existing
                            break;
                        }
                    }
                }
                else
                {
                    FlipbookGeneratorLog.Warn("音声の抽出に失敗しました。動画に音声トラックがない可能性があります。");
                }
            }
        }

        private static string AssetPathOrNull(DefaultAsset asset)
        {
            if (asset == null) return null;
            return AssetDatabase.GetAssetPath(asset);
        }
    }
}
