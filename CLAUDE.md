## Goal

動画ファイルまたはPNG連番からスプライトシートとフリップブックマテリアルを生成する Unity Editor ツール。

## Current State

3モード（Texture2DArray / LilToon / MultiPageSequence）× 2入力モード（VideoFile / PngSequence）が動作。SpriteSheet モードは除外済み。version 1.1.0 まで公開完了（GitHub Release / VPM listing / BOOTH、HTML 使い方ガイド v1.1 同梱）。

機能の全量仕様は正本を各所へ委譲 — ユーザー面（入力・出力管理・Prefab/MA・UI・上級設定）は `Documentation~/notes/guide-source/`、非自明な設計判断は `Documentation~/notes/technical/` + 本ファイル下部「技術知見（flipbook 固有）」節、各ファイルの役割は下部「ファイル構成」節、実挙動はコード直読が正本。後回しは knowledge-base `next-phase/tool-dev.md`「Flipbook」節。

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

### テクスチャメモリー見積もり
- EstimateTextureBytes の frameSize は Builder の DefaultFrameSize (256) を起点にする。_extractMaxResolution は FFmpeg 抽出時の解像度であり、シート書き込み時のフレームサイズではない
- VRChat テクスチャメモリー ≈ DXT5 (1 byte/pixel)。RGBA32 未圧縮 (4 bytes/pixel) はユーザーの直感（VRChat パフォーマンスランク画面の値）と乖離する
- EstimateTextureBytes() に計算を一本化し、OnGUI / DryRun / Generate の全箇所で再利用する

### Audio 制御: m_IsActive vs m_Enabled
- AudioSource.m_Enabled の OFF→ON では playOnAwake は発火しない（コンポーネント有効化のみ）
- GameObject.m_IsActive の OFF→ON で playOnAwake が発火し、再生が先頭から始まる
- MultiPageSequence の Audio 制御は m_IsActive を使用（全ステートで明示キーフレーム）

## ファイル構成

### Editor (namespace: Sebanne.FlipbookMaterialGenerator.Editor)
- `Editor/FlipbookMaterialGeneratorWindow.cs` — メインウィンドウ。5セクション構成（入力設定・出力設定・Prefab/MA設定・実行・上級設定）。ScrollView + helpBox 枠。DrawSectionHeader / DrawSubInfo / DrawOutputPathPreview ヘルパー。
- `Editor/Core/FlipbookConstants.cs` — パス文字列・オブジェクト名・Animatorパラメータ名・シェーダープロパティ名の定数定義。
- `Editor/Core/FlipbookVideoConverter.cs` — FFmpeg 動画入力。IsFFmpegAvailable / Probe(ffprobe、音声トラック検出含む) / ExtractFrames(PNG抽出→Texture2D[]、ハッシュベースキャッシュ対応) / ExtractAudio(WAV抽出→AudioClip、キャッシュキー照合対応)。
- `Editor/Core/FlipbookFrameLoader.cs` — PNG 連番読み込み。Load（maxFrames パラメータで上限指定）と LoadAll（上限なし、MPS 用）。AssetDatabase 経由。読み込み失敗フレームはスキップ続行（Warn ログ）。Generated_Flipbook 除外フィルタ付き。
- `Editor/Core/FlipbookSheetBuilder.cs` — スプライトシート生成。自動グリッド計算、最大 2048x2048。FlipbookSheetResult を返す。
- `Editor/Core/FlipbookMaterialBuilder.cs` — マテリアル生成。BuildFromArray / BuildForSequence / BuildForLilToon の 3 メソッド。既存マテリアルは CopyPropertiesFromMaterial で GUID 保持更新。
- `Editor/Core/FlipbookPrefabBuilder.cs` — Prefab 生成。Build（3モード用）と BuildMultiPage（MultiPageSequence用）。MA Menu 構造（MenuInstaller + SubMenu MenuItem + ObjectToggle。MultiPageSequence は Toggle + Loop + Reset、3モードは Toggle のみ）を生成、MergeAnimator をアタッチ（リフレクション方式、optional、MultiPageSequence のみ）。
- `Editor/Core/FlipbookPageSplitter.cs` — MultiPageSequence 用。PNG連番を複数ページ（スプライトシート）に分割。
- `Editor/Core/FlipbookAnimationBuilder.cs` — MultiPageSequence 用。各ページのAnimationClipを生成。
- `Editor/Core/FlipbookAnimatorBuilder.cs` — MultiPageSequence 用。AnimatorControllerを生成（Idle + Page + ResetPage1 ステート、FlipbookEnabled / FlipbookLoop / FlipbookReset パラメータ）。
- `Editor/Core/FlipbookResultInfo.cs` — 結果ダイアログ用データクラス（IsDryRun / Success / ModeName / Lines / PingAssetPath）。
- `Editor/Core/FlipbookFileUtility.cs` — 共通ユーティリティ。DeleteFileAndMeta / DeleteFolderAndMeta（File.Delete ベース、ゴミ箱を経由しない完全削除）。EnsureAssetFolderExists（AssetDatabase.CreateFolder ベースの再帰フォルダ作成）。MakeReadable（Texture2D を読み取り可能にリサイズ）。
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
- Editor-only ファイルの namespace は `Sebanne.FlipbookMaterialGenerator.Editor` に統一する。

## 次フェーズ候補

knowledge-base `next-phase/tool-dev.md`「Flipbook」節（後回しの正本）を参照。旧 Notion 次フェーズ候補 DB は凍結。
