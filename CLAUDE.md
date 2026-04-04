## Goal

動画ファイルまたはPNG連番からスプライトシートとフリップブックマテリアルを生成する Unity Editor ツール。

## Current State

3モード（Texture2DArray / LilToon / MultiPageSequence）× 2入力モード（VideoFile / PngSequence）が動作する状態。SpriteSheet モードは除外済み。

version 1.0.0 リリース完了。GitHub Release / VPM listing / VCC 導入確認済み。
BOOTH 商品ページ作成が残っている。

### 入力
- VideoFile モード: Assets 内動画ファイルを ObjectField で選択、ffprobe で参考情報表示、FFmpeg で PNG 抽出・音声抽出・トリミング対応（-ss/-t）
- FFmpeg PNG 抽出キャッシュ: %TEMP%/FlipbookFrames/{hash}/ にキャッシュ。動画パス+FPS+トリム+解像度+最終更新日時の MD5 ハッシュ。一致すれば抽出スキップ
- 音声抽出キャッシュ: audio_cache_key.txt でキャッシュキー照合（動画パス+トリム設定+最終更新日時）。一致すれば FFmpeg スキップ
- PngSequence モード: PNG 連番フォルダ指定

### 出力管理
- スロット方式フォルダ構成（Generated_Flipbook/{番号}_{出力名}/）
- 出力先3モード（SourceRelative / ToolDefault / Custom）
- スロットブラウザ UI（概要表示・フォルダを開く・名前クリックで選択・削除機能付き）
- 同一スロットのモード変更時に旧生成物を全削除（Audio/ フォルダは保護）
- Texture2DArray / LilToon モードはフレーム上限ガードあり（maxSheetSize から逆算、超過時 Generate ブロック）

### Prefab 生成・MA 連携
- Prefab 生成対応（FlipbookPrefabBuilder）
- MA optional 対応（リフレクション方式）
- MultiPageSequence: MergeAnimator / ObjectToggle / Menu の 3 bool 制御
- 3モード: ObjectToggle / Menu の 2 bool 制御
- MA Menu 構造: MenuInstaller + SubMenu MenuItem → Toggle 子オブジェクト（3モード）/ Toggle + Loop + Reset 子オブジェクト（MultiPageSequence）
- AudioSource 対応（Audio/ 子オブジェクト）。3モード: ObjectToggle 連動 / MultiPageSequence: Animation layer で m_IsActive 制御
- 子オブジェクト並び順: MA Menu(0) → Audio(1) → Quad/Pages(2)

### MultiPageSequence 固有
- PNG 連番を複数スプライトシートに自動分割
- AnimatorController でシームレスループ再生
- Idle ステート（全ページ OFF）をデフォルトステートとして追加
- AnimationClip は writeDefaultValues 非依存設計（自分 ON・他全ページ OFF・Audio m_IsActive 明示制御）
- FlipbookEnabled（Bool）/ FlipbookLoop（Bool, default=true）/ FlipbookReset（Bool）の 3 パラメータ常時生成
- ResetPage1 ステート: Audio m_IsActive OFF→ON で再生位置リセット、再生後 Page2 へ遷移
- AnimatorController レイヤー名: "Flipbook"

### UI
- 5セクション構成（入力設定・出力設定・Prefab/MA設定・実行・上級設定）、helpBox 枠で Skinned Mesh Mirror 式ブロック分け
- セクション見出しは DrawSectionHeader（boldLabel + miniLabel 補助文）
- DrawSubInfo ヘルパーで情報表示用 miniLabel を共通化
- FPS・トリミングは入力設定セクション内、MPS 分割プレビューはフレーム推定の直後
- 出力モード Popup 直下に Info HelpBox でモード説明（再生方式・重さ・用途・制約）
- 出力先パスは Skinned Mesh Mirror 式（「現在の出力先」ラベル + 完全パス + 補助文）
- スロット一覧はスロット選択の直下（Foldout）
- 入力不足案内は Dry Run / Generate ボタンの上
- 上級設定は最下部 Foldout
- OnGUI 全体を ScrollView で囲む
- HelpBox は警告・エラー専用。説明・情報表示は DrawSubInfo（miniLabel）
- ラベルはツール概念語を日本語化済み（出力モード、入力フォルダ、出力フォルダ、PNG連番 FPS）
- コンポーネント固有名（MA, Prefab, AudioClip 等）は英語のまま
- プリセット UI（おすすめ / カスタム）全3モード対応
- FPS 補助計算 UI（分秒入力・入力フォルダから自動カウント）
- domain reload 対応済み（SerializeField + _videoInfo re-probe）
- enableMenu=false 時は MA Menu を生成せず ObjectToggle を root に直接アタッチ

### アセット上書き
- 削除は全箇所 FlipbookFileUtility 経由（File.Delete + .meta 削除、ゴミ箱を経由しない完全削除）
- Material: CopyPropertiesFromMaterial で GUID 保持
- Texture2DArray / AnimationClip: 完全削除 → Refresh → CreateAsset
- AnimatorController: 完全削除 → Refresh → CreateAnimatorControllerAtPath
- PNG: File.WriteAllBytes（.meta 保持で GUID 不変）

### その他
- ミップストリーミング常時 ON（TextureImporter.streamingMipmaps = true）
- FrameLoader: 読み込み失敗スキップ続行（Warn ログ）
- FlipbookConstants.cs でパス文字列・オブジェクト名・Animator パラメータ名・シェーダープロパティ名を定数化
- MA enum は Enum.Parse + try-catch（数値直書き禁止）

## 技術知見（flipbook 固有）

### LilToon
- Quad 使用時は _Cull=0（両面描画）必須（Quad の法線が +Z でカメラに背を向けるため）
- Main2nd DecalAnimation に必要なキーワード: _COLORADDSUBDIFF_ON + _SUNDISK_NONE
  → material.EnableKeyword() で設定（SetFloat だけでは不足）
- Shader.Find("lilToon") で可用性検出（リフレクションより簡潔）

### シートサイズ
- シートサイズを変えても総テクスチャ量はほぼ不変（1枚が重い vs 枚数が多い）
- パフォーマンスに効くのは FPS と元 PNG の解像度 × フレーム数
- シートサイズは「元 PNG に対して十分な器を確保するため」と理解するのが正確
- ほとんどのユーザーは 2048 固定で困らない
- ツールの最大シートサイズ（生成ピクセルサイズの上限）と Unity の Max Size（ビルド時の圧縮上限）は別物

### PNG Sequence FPS
- UI の FPS は元動画の FPS ではなく PNG 書き出し時の FPS を入力する
- FFmpeg 等で間引かれる場合があるため「枚数 ÷ 秒数」で確認

### MA ObjectToggle / Menu 配置
- ObjectToggle のターゲットは ObjectToggle より下の階層に置く（先祖オブジェクトは不可）
- AvatarObjectReference は referencePath に相対パス文字列を直接セット + targetObject（NonPublic）に GameObject を直接セット
  → Set(GameObject) を Prefab 保存前の一時オブジェクトに呼ぶとパス解決が壊れる
- MenuInstaller は root 直付けせず専用子オブジェクト（例: MA Menu/）に置く
  → MenuInstaller + SubMenu 型 MenuItem を同じオブジェクトにアタッチし、子の MenuItem を束ねる
- subMenuSource は Children に明示設定（デフォルト値に依存しない）

### FX レイヤーと表示制御
- MergeAnimator で FX 統合された AnimatorController はアバタールートの Animator で常時動作
- ObjectToggle でオフにしても FX 再生は止まらない
- 「オフ中に止めて途中から再開」は VRChat + MergeAnimator 環境では実現困難
- keepAnimatorStateOnDisable: MA なし・Animator 単体運用時にオブジェクトをオフにして途中から再開する手段として使える可能性があるが、VRChat 環境での動作は未検証・不安定の可能性あり
- FX 制御（EX メニュー等）と Animator 停止（keepAnimatorState）は構造的に排他。MergeAnimator で FX 統合すると Animator は常時動作し止められない

### writeDefaultValues 非依存設計
- 各 AnimationClip が「自分 ON・他全ページ OFF」を明示キーフレームで制御
- Audio m_IsActive も全ステート（Idle/Page1-N/ResetPage1）で明示キーフレームを持つ
- WD = true のままでも問題ない（暗黙のデフォルト復帰に頼っていない）
- 一部ステートだけで m_IsActive を操作すると WD=true の他ステートでデフォルト値に書き戻されるため、全ステートに明示キーフレームが必要

### 音声と映像の同期制約
- Texture2DArray / LilToon はシェーダーベースアニメーション（_Time 依存）で AnimatorController を使わないため、音声との再生同期は不可能
- Audio 用途はループ BGM・環境音など同期不要のものに限られる
- MultiPageSequence のみ Animator 制御なので Animation layer 連動で映像・音声の同期が可能

### Audio 制御: m_IsActive vs m_Enabled
- AudioSource.m_Enabled の OFF→ON では playOnAwake は発火しない（コンポーネント有効化のみ）
- GameObject.m_IsActive の OFF→ON で playOnAwake が発火し、再生が先頭から始まる
- MultiPageSequence の Audio 制御は m_IsActive を使用（全ステートで明示キーフレーム）

## ファイル構成

### Editor (namespace: Sebanne.FlipbookMaterialGenerator.Editor)
- `Editor/FlipbookMaterialGeneratorWindow.cs` — メインウィンドウ。5セクション構成（入力設定・出力設定・Prefab/MA設定・実行・上級設定）。ScrollView + helpBox 枠。DrawSectionHeader / DrawSubInfo / DrawOutputPathPreview ヘルパー。
- `Editor/Core/FlipbookConstants.cs` — パス文字列・オブジェクト名・Animatorパラメータ名・シェーダープロパティ名の定数定義。
- `Editor/Core/FlipbookVideoConverter.cs` — FFmpeg 動画入力。IsFFmpegAvailable / Probe(ffprobe、音声トラック検出含む) / ExtractFrames(PNG抽出→Texture2D[]、ハッシュベースキャッシュ対応) / ExtractAudio(WAV抽出→AudioClip、キャッシュキー照合対応)。
- `Editor/Core/FlipbookFrameLoader.cs` — PNG 連番読み込み。AssetDatabase 経由、上限なし（LoadAll）。読み込み失敗フレームはスキップ続行（Warn ログ）。Generated_Flipbook 除外フィルタ付き。
- `Editor/Core/FlipbookSheetBuilder.cs` — スプライトシート生成。自動グリッド計算、最大 2048x2048。FlipbookSheetResult を返す。
- `Editor/Core/FlipbookMaterialBuilder.cs` — マテリアル生成。BuildFromArray / BuildForSequence / BuildForLilToon の 3 メソッド。既存マテリアルは CopyPropertiesFromMaterial で GUID 保持更新。
- `Editor/Core/FlipbookPrefabBuilder.cs` — Prefab 生成。Build（3モード用）と BuildMultiPage（MultiPageSequence用）。MA Menu 構造（MenuInstaller + SubMenu MenuItem + ObjectToggle。MultiPageSequence は Toggle + Loop + Reset、3モードは Toggle のみ）を生成、MergeAnimator をアタッチ（リフレクション方式、optional、MultiPageSequence のみ）。
- `Editor/Core/FlipbookPageSplitter.cs` — MultiPageSequence 用。PNG連番を複数ページ（スプライトシート）に分割。
- `Editor/Core/FlipbookAnimationBuilder.cs` — MultiPageSequence 用。各ページのAnimationClipを生成。
- `Editor/Core/FlipbookAnimatorBuilder.cs` — MultiPageSequence 用。AnimatorControllerを生成（Idle + Page + ResetPage1 ステート、FlipbookEnabled / FlipbookLoop / FlipbookReset パラメータ）。
- `Editor/Core/FlipbookResultInfo.cs` — 結果ダイアログ用データクラス（IsDryRun / Success / ModeName / Lines / PingAssetPath）。
- `Editor/Core/FlipbookFileUtility.cs` — ファイル/フォルダ削除ユーティリティ。DeleteFileAndMeta / DeleteFolderAndMeta。File.Delete ベース（ゴミ箱を経由しない完全削除）。
- `Editor/Diagnostics/FlipbookGeneratorLog.cs` — ログユーティリティ。prefix `[FlipbookMaterialGenerator]`。Enabled ゲート付き（Info/Warn のみ、Error は常時出力）。

### Runtime
- `Runtime/FlipbookSequenceShader.shader` — MultiPageSequence 用シェーダー。
- `Runtime/FlipbookArrayShader.shader` — Texture2DArray 用シェーダー。

### asmdef
- `Editor/Sebanne.FlipbookMaterialGenerator.Editor.asmdef`
- `Runtime/Sebanne.FlipbookMaterialGenerator.asmdef`

## Current Blocker

なし。

## Rules

- 非破壊を最優先にし、既存データや既存設定を直接書き換える前に確認手段を用意する。
- Dry Run優先で、まずは変更予定の内容を確認できる導線を用意する。
- まず短い plan を出してから作業する。
- commit / push は明示的な指示があるまで行わない。
- Editor-only ファイルの namespace は `Sebanne.FlipbookMaterialGenerator.Editor` に統一する。

## 次フェーズ候補（後回し）
- Texture2DArray clamp=64 と CalculateMaxFrames のズレ整理（安全側、実害なし）
- FPS 計算機の折りたたみ化（PNG モード時に目立ちすぎ）
- lilToon プロパティ名ハードコード（バージョンアップ時に手動確認の運用）
- おすすめプリセットの設定値を詰める（UI改善）
- 生成前の容量見積もり表示（テクスチャサイズ、できればダウンロードサイズ）
- Quad 裏表問題の改善（Edit Only ダミーオブジェクト / ガイド枠の案）
