## Goal

PNG連番からスプライトシートとフリップブックマテリアルを生成する Unity Editor ツール。

## Current State

全コンポーネント実装済み・動作確認済み
（SpriteSheet / Texture2DArray / LilToon / MultiPageSequence）。
Prefab生成対応済み（FlipbookPrefabBuilder）。
MA optional 対応（リフレクション方式）。
MultiPageSequence: PNG連番を複数スプライトシートに自動分割し、
AnimatorControllerでシームレスにループ再生する仕組みを生成。
MA Merge Animator（FX, Relative, matchAvatarWriteDefaults）対応済み。

### PNG Sequence FPS について
- UI の「PNG Sequence FPS」は元動画のFPSではなく、PNG書き出し時のFPSを入力する
- FFmpegなどの書き出し設定で間引かれる場合があるため、
  「枚数 ÷ 動画秒数」で実際の書き出しFPSを確認できる

### 次フェーズ候補（後回し）
- シート上限サイズのUI選択
- OutputFolder規定値を Generated/ に変更
- 生成物をサブフォルダに整理
- RendererPathの自動生成
- MA Object Toggleで再生ON/OFF制御
- テクスチャのミップストリーミング対応
- PNG枚数と動画秒数からFPSを自動計算する補助機能
- FFmpeg連携による動画入力対応
- ウィンドウ内で過去生成物・フォルダ参照UI

### Editor (namespace: Sebanne.FlipbookMaterialGenerator.Editor)
- `Editor/FlipbookMaterialGeneratorWindow.cs` — メインウィンドウ（`Tools/Sebanne/Flipbook Material Generator`）。入力/出力フォルダ、OutputMode 切り替え（SpriteSheet / Texture2DArray / LilToon）、FPS 設定、Prefab生成チェックボックス、Dry Run / Generate ボタン。
- `Editor/Core/FlipbookFrameLoader.cs` — PNG 連番読み込み。AssetDatabase 経由、最大 64 フレーム。
- `Editor/Core/FlipbookSheetBuilder.cs` — スプライトシート生成。自動グリッド計算、最大 2048x2048。FlipbookSheetResult を返す。
- `Editor/Core/FlipbookArrayBuilder.cs` — Texture2DArray 生成。各フレームをスライスとして格納、.asset で保存。FlipbookArrayResult を返す。
- `Editor/Core/FlipbookMaterialBuilder.cs` — マテリアル生成。Build（SpriteSheet用）/ BuildFromArray（Texture2DArray用）/ BuildForLilToon（lilToon Main2nd DecalAnimation 用）。
- `Editor/Core/FlipbookPrefabBuilder.cs` — Prefab 生成。Quad + Material の Prefab を出力。MA 検出時は MenuInstaller + ObjectToggle をアタッチ（リフレクション方式、optional）。
- `Editor/Core/FlipbookPageSplitter.cs` — MultiPageSequence 用。PNG連番を複数ページ（スプライトシート）に分割。
- `Editor/Core/FlipbookAnimationBuilder.cs` — MultiPageSequence 用。各ページのAnimationClipを生成。
- `Editor/Core/FlipbookAnimatorBuilder.cs` — MultiPageSequence 用。AnimatorControllerを生成、MA Merge Animatorをアタッチ。
- `Editor/Diagnostics/FlipbookGeneratorLog.cs` — ログユーティリティ。prefix `[FlipbookMaterialGenerator]`。

### Runtime
- `Runtime/FlipbookShader.shader` — Unlit フリップブックシェーダー（"Sebanne/FlipbookShader"）。スプライトシート用、_Time.y ベースループ再生、Cutout、Quest 対応。
- `Runtime/FlipbookArrayShader.shader` — Unlit フリップブックシェーダー（"Sebanne/FlipbookArrayShader"）。Texture2DArray 用、UNITY_SAMPLE_TEX2DARRAY でスライス直接参照。
- `Runtime/FlipbookSequenceShader.shader` — MultiPageSequence 用シェーダー。

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
