## Goal

PNG連番からスプライトシートとフリップブックマテリアルを生成する Unity Editor ツール。

## Current State

全コンポーネント実装済み・動作確認済み（SpriteSheet / Texture2DArray / LilToon）。
Prefab生成対応済み（FlipbookPrefabBuilder）。
MA optional 対応（リフレクション方式）。
lilToon optional 対応（Shader.Find 方式、SpriteSheet モードのみ）。
次フェーズ候補: 自動分割+AnimatorController。

### Editor (namespace: Sebanne.FlipbookMaterialGenerator.Editor)
- `Editor/FlipbookMaterialGeneratorWindow.cs` — メインウィンドウ（`Tools/Sebanne/Flipbook Material Generator`）。入力/出力フォルダ、OutputMode 切り替え（SpriteSheet / Texture2DArray / LilToon）、FPS 設定、Prefab生成チェックボックス、Dry Run / Generate ボタン。
- `Editor/Core/FlipbookFrameLoader.cs` — PNG 連番読み込み。AssetDatabase 経由、最大 64 フレーム。
- `Editor/Core/FlipbookSheetBuilder.cs` — スプライトシート生成。自動グリッド計算、最大 2048x2048。FlipbookSheetResult を返す。
- `Editor/Core/FlipbookArrayBuilder.cs` — Texture2DArray 生成。各フレームをスライスとして格納、.asset で保存。FlipbookArrayResult を返す。
- `Editor/Core/FlipbookMaterialBuilder.cs` — マテリアル生成。Build（SpriteSheet用）/ BuildFromArray（Texture2DArray用）/ BuildForLilToon（lilToon Main2nd DecalAnimation 用）。
- `Editor/Core/FlipbookPrefabBuilder.cs` — Prefab 生成。Quad + Material の Prefab を出力。MA 検出時は MenuInstaller + ObjectToggle をアタッチ（リフレクション方式、optional）。
- `Editor/Diagnostics/FlipbookGeneratorLog.cs` — ログユーティリティ。prefix `[FlipbookMaterialGenerator]`。

### Runtime
- `Runtime/FlipbookShader.shader` — Unlit フリップブックシェーダー（"Sebanne/FlipbookShader"）。スプライトシート用、_Time.y ベースループ再生、Cutout、Quest 対応。
- `Runtime/FlipbookArrayShader.shader` — Unlit フリップブックシェーダー（"Sebanne/FlipbookArrayShader"）。Texture2DArray 用、UNITY_SAMPLE_TEX2DARRAY でスライス直接参照。

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
