## Goal

PNG連番からスプライトシートとフリップブックマテリアルを生成する Unity Editor ツール。

## Current State

スプライトシート / Texture2DArray の2モード実装完了。Unity上での動作テストが次のステップ。

### Editor (namespace: Sebanne.FlipbookMaterialGenerator.Editor)
- `Editor/FlipbookMaterialGeneratorWindow.cs` — メインウィンドウ（`Tools/Sebanne/Flipbook Material Generator`）。入力/出力フォルダ、OutputMode 切り替え（SpriteSheet / Texture2DArray）、FPS 設定、Dry Run / Generate ボタン。
- `Editor/Core/FlipbookFrameLoader.cs` — PNG 連番読み込み。AssetDatabase 経由、最大 64 フレーム。
- `Editor/Core/FlipbookSheetBuilder.cs` — スプライトシート生成。自動グリッド計算、最大 2048x2048。FlipbookSheetResult を返す。
- `Editor/Core/FlipbookArrayBuilder.cs` — Texture2DArray 生成。各フレームをスライスとして格納、.asset で保存。FlipbookArrayResult を返す。
- `Editor/Core/FlipbookMaterialBuilder.cs` — マテリアル生成。Build（SpriteSheet用）/ BuildFromArray（Texture2DArray用）。
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
