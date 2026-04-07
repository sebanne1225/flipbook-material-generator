# Changelog

このファイルは `Flipbook Material Generator` の変更履歴を管理します。

## [1.1.0] - 2026-04-07

### Fixed

- Texture2DArray / LilToon のフレーム上限が 64 にハードコードされていた問題を修正（最大シートサイズに応じた動的計算に統一）

### Changed

- フォルダ作成・テクスチャ読み込みの重複コードを FlipbookFileUtility に統合
- 未使用のデッドコード（TryAttachModularAvatar）を削除
- GUIStyle の毎フレーム生成を static キャッシュに変更
- プリセット（おすすめ / カスタム）を常時表示に変更し、Prefab 生成・自動分割もプリセット対象に追加
- FPS 計算機を折りたたみ化（デフォルト閉）
- Dry Run / Generate ダイアログの容量見積もりを RGBA32 から DXT5 表示に変更

### Added

- 実行セクションに推定テクスチャメモリー増加量をリアルタイム表示（PC/DXT5）
- Texture2DArray / LilToon モードに最大秒数ガイドを表示

## [1.0.0] - 2026-04-04

### Added

- 3 つの出力モードに対応（MultiPageSequence / Texture2DArray / LilToon）
- 動画ファイル入力に対応（FFmpeg 経由の PNG 抽出・音声抽出・トリミング）
- PNG 連番フォルダ入力に対応
- Prefab 生成・Modular Avatar 連携（MergeAnimator / ObjectToggle / Menu、リフレクション方式）
- AudioSource 対応（音声同期は MultiPageSequence のみ）
- Dry Run / Generate ワークフロー（結果ダイアログ表示）
- スロット方式の出力管理（自動番号付与、上書き、削除）
- 出力先 3 モード（元ソース直下 / ツール共通フォルダ / フォルダを指定）
- プリセット UI（おすすめ / カスタム）
- FPS 補助計算機（PNG 連番モード）
- FFmpeg 抽出キャッシュ（PNG / 音声）

### Changed

- UI を 5 セクション構成に整理（入力設定・出力設定・Prefab/MA 設定・実行・上級設定）
- HelpBox を警告・エラー専用に整理し、説明表示は miniLabel に変更
- ラベルをツール概念語は日本語、コンポーネント固有名は英語に統一
- 生成物の削除を File.Delete ベースに変更（ゴミ箱蓄積を防止）
- README / TOOL_INFO / package.json を公開向けに整備

### Notes

- MA は optional（未導入でも Prefab 生成は可能）
- lilToon は LilToon モード使用時のみ必要
- 動画入力には FFmpeg のインストールが必要

## [0.1.0] - 2026-03-28

### Added

- テンプレートから初期立ち上げ。repo 名・package id・namespace・menu path をツール固有の識別子に置換
- `package.json`、asmdef、README、各種メモを Flipbook Material Generator 用に更新
