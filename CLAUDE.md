## Goal

PNG連番からスプライトシートとフリップブックマテリアルを生成する Unity Editor ツール。

## Current State

MultiPageSequence モード実装済み・動作確認済み。
出力先モード（元ソース直下 / ツール共通フォルダ / フォルダ指定）実装済み。
MultiPageSequence のみサブフォルダ構成（Sheets/ Materials/ Animation/ Prefabs/）に整理。
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
- シート上限サイズのUI選択（改善）
- RendererPathをPrefab構造から自動生成する（改善）
- MA Object Toggleで再生ON/OFF制御を設定する（改善）
- テクスチャのミップストリーミングをONにする（改善）
- PNG枚数と動画秒数からFPSを自動計算する補助機能（改善）
- SpriteSheet / Texture2DArray / LilToon モードの動作確認（素材を変えて検証）（確認）
- FFmpeg連携による動画入力対応（将来拡張）
- ウィンドウ内で過去生成物・フォルダ参照UI（将来拡張）

### Editor (namespace: Sebanne.FlipbookMaterialGenerator.Editor)
- `Editor/FlipbookMaterialGeneratorWindow.cs` — メインウィンドウ（`Tools/Sebanne/Flipbook Material Generator`）。入力/出力フォルダ、出力先モード切り替え（元ソース直下 / ツール共通フォルダ / フォルダ指定）、FPS 設定、Prefab生成チェックボックス、Dry Run / Generate ボタン。
- `Editor/Core/FlipbookFrameLoader.cs` — PNG 連番読み込み。AssetDatabase 経由、上限なし（LoadAll）。
- `Editor/Core/FlipbookSheetBuilder.cs` — スプライトシート生成。自動グリッド計算、最大 2048x2048。FlipbookSheetResult を返す。
- `Editor/Core/FlipbookMaterialBuilder.cs` — マテリアル生成。BuildForSequence（MultiPageSequence用）。
- `Editor/Core/FlipbookPrefabBuilder.cs` — Prefab 生成。BuildMultiPage で複数ページ Prefab を出力。MA 検出時は MenuInstaller + MergeAnimator をアタッチ（リフレクション方式、optional）。
- `Editor/Core/FlipbookPageSplitter.cs` — MultiPageSequence 用。PNG連番を複数ページ（スプライトシート）に分割。
- `Editor/Core/FlipbookAnimationBuilder.cs` — MultiPageSequence 用。各ページのAnimationClipを生成。
- `Editor/Core/FlipbookAnimatorBuilder.cs` — MultiPageSequence 用。AnimatorControllerを生成、MA Merge Animatorをアタッチ。
- `Editor/Diagnostics/FlipbookGeneratorLog.cs` — ログユーティリティ。prefix `[FlipbookMaterialGenerator]`。

### Runtime
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
