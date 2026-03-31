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
最大シートサイズ選択UI（512/1024/2048/4096、デフォルト2048）実装済み。
FlipbookPageSplitter.CalculateFramesPerPage も maxSheetSize と連動済み。
最大シートサイズ・1ページ最大フレーム数を上級設定（折りたたみ）に移動済み。
FPS補助計算UI（分秒入力・Input Folderから自動カウント）実装済み。
ミップストリーミング常時ON実装済み（TextureImporter.streamingMipmaps = true）。
Prefab生成UIをMA Merge Animator / MA Object Toggleの2つに分離実装済み。
MultiPageSequenceのAnimationClipを「自分ON・他全ページOFF」に修正済み
（writeDefaultValues非依存。FlipbookAnimationBuilder で全Pageを明示制御）。
PlaybackMode（Loop / ManualReset）再生モード選択UI実装済み。
AnimatorController に Idle ステート（全ページOFF）をデフォルトステートとして追加済み。
FlipbookToggle（Bool）パラメータ制御実装済み。
ManualReset 時は FlipbookReset（Bool）も追加。
AnyState → Idle 遷移削除、Pages/ 表示制御は MA ObjectToggle に委譲。
MA Menu 構造実装済み
（MenuInstaller + SubMenu MenuItem → Toggle / Reset 子オブジェクト）。
Toggle の MA MenuItem パラメータ名: FlipbookToggle。
Reset の MA MenuItem パラメータ名: FlipbookReset（ManualReset 時のみ生成）。
Pages/ 子オブジェクト構造実装済み（pagesObj.SetActive(false) でデフォルト非表示）。
AnimatorController レイヤー名を "Flipbook" にリネーム済み。
OnEnable で _framesPerPage 初期化（UI 空白バグ修正）済み。
プリセット UI（おすすめ / カスタム）実装済み。MAMode は廃止。
_enableMergeAnimator / _enableObjectToggle / _enableMenu の 3 bool で MA 連携を制御。
enableMenu = false 時は ObjectToggle + MA Menu 構造を丸ごとスキップ。

### 最大シートサイズについて
- シートサイズを変えても総テクスチャ量はほぼ変わらない（1枚が重くなる vs 枚数が増える のトレードオフ）
- パフォーマンスに一番効くのは FPS と 元 PNG の解像度 × フレーム数
- シートサイズは「元 PNG に対して十分な器を確保するためにある」と理解するのが正確
- ほとんどのユーザーは 2048 固定で困らないため、上級設定に隠している
- ツールの最大シートサイズ（生成ピクセルサイズの上限）と Unity の Max Size（ビルド時の圧縮上限）は別物。元テクスチャが Unity の Max Size より小さければ影響なし

### PNG Sequence FPS について
- UI の「PNG Sequence FPS」は元動画のFPSではなく、PNG書き出し時のFPSを入力する
- FFmpegなどの書き出し設定で間引かれる場合があるため、
  「枚数 ÷ 動画秒数」で実際の書き出しFPSを確認できる

### 次フェーズ候補（後回し）
- 途中から再開プリセットの追加（改善）
  ※ keepAnimatorStateOnDisable ベース。VRChat 環境での動作未検証のため保留中。
- OffReset モード（Object Toggle OFF で frame0 へ戻る）（改善）
  ※ VRC 対応には bool パラメータ + Idle ステート +
  MA パラメータドライバー連携が必要。実装コストが高いため次フェーズ
- keepAnimatorStateOnDisable による途中再開（改善）
  ※ MA なし・Animator 単体運用時に root をオンオフして途中から再開する方式
  VRChat 環境での動作が未検証・不安定の可能性あり
- Object Toggle の音源対応（改善）
  ※ ObjectToggle の Objects リストに AudioSource も自動追加する
- SpriteSheet / Texture2DArray / LilToon モードの動作確認（確認）
- FFmpeg連携による動画入力対応（将来拡張）
- ウィンドウ内で過去生成物・フォルダ参照UI（将来拡張）
- UI説明文・ドキュメントの整備（改善）

### Editor (namespace: Sebanne.FlipbookMaterialGenerator.Editor)
- `Editor/FlipbookMaterialGeneratorWindow.cs` — メインウィンドウ（`Tools/Sebanne/Flipbook Material Generator`）。入力/出力フォルダ、出力先モード切り替え（元ソース直下 / ツール共通フォルダ / フォルダ指定）、FPS 設定、Prefab生成チェックボックス、Dry Run / Generate ボタン。
- `Editor/Core/FlipbookFrameLoader.cs` — PNG 連番読み込み。AssetDatabase 経由、上限なし（LoadAll）。
- `Editor/Core/FlipbookSheetBuilder.cs` — スプライトシート生成。自動グリッド計算、最大 2048x2048。FlipbookSheetResult を返す。
- `Editor/Core/FlipbookMaterialBuilder.cs` — マテリアル生成。BuildForSequence（MultiPageSequence用）。
- `Editor/Core/FlipbookPrefabBuilder.cs` — Prefab 生成。BuildMultiPage で複数ページ Prefab を出力。MA Menu 構造（MenuInstaller + SubMenu MenuItem + ObjectToggle + Reset）を生成、MergeAnimator をアタッチ（リフレクション方式、optional）。
- `Editor/Core/FlipbookPageSplitter.cs` — MultiPageSequence 用。PNG連番を複数ページ（スプライトシート）に分割。
- `Editor/Core/FlipbookAnimationBuilder.cs` — MultiPageSequence 用。各ページのAnimationClipを生成。
- `Editor/Core/FlipbookAnimatorBuilder.cs` — MultiPageSequence 用。AnimatorControllerを生成（Idle + Page ステート、FlipbookToggle / FlipbookReset パラメータ）。
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
