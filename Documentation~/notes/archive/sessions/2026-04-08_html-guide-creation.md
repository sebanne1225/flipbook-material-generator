# HTML ガイド作成セッション (2026-04-08)

## やったこと

### notes フォルダ再構成
- フラット配置の 13 ファイルを 4 サブフォルダに分類（sessions / technical / guide-source / archive）
- 命名規則を統一（sessions: 日付プレフィックス、technical/guide-source: トピック名のみ、ケバブケース）
- knowledge base に DOCUMENTATION_NOTES_GUIDELINES.md を新規作成

### PDF ガイド作成 → HTML に方向転換
- 当初は guide-source/ の 11 章骨子 → 肉付け → PDF の流れで進行
- Chrome headless で PDF 生成 → フッター制御・目次リンクの問題が発覚
- fpdf2 に切り替え → ページ番号・目次リンクは成功したが、レイアウト制御の柔軟性に限界
- 最終的に HTML 1 ファイル完結方式に方向転換

### PDF の問題点
- Chrome headless: フッターにローカルパス・日時が表示される。`--print-to-pdf-no-header` の挙動が不安定
- Chrome headless: CSS `@page` によるページ番号挿入が未サポート
- fpdf2: 日本語等幅フォント（Courier 不可 → BIZ UD Gothic で回避）の問題
- 共通: 画像サイズ調整が弱い、レスポンシブ不可、目次リンクの信頼性が低い

### HTML 方式の利点
- 1 ファイル完結（画像 Base64 埋め込み、CSS/JS インライン）
- BOOTH 同梱しやすい（HTML 1 ファイルだけで動く）
- 目次リンク確実（`<a href="#id">` + `id` 属性）
- レスポンシブ対応（PC サイドバー + モバイルハンバーガーメニュー）
- オフライン対応（外部リソース参照なし）
- レイアウト制御が柔軟（CSS で自由に調整可能）

### 成果物
- flipbook-guide.html: 905KB、11章、スクショ 22 枚埋め込み
- サイドバー目次ナビ（固定 + スクロール追従）
- 章見出し: 左ブルーボーダー + 背景帯

### スクショ撮影・選定
- Documentation~/images/ に 22 枚（01〜24、09/13 欠番）
- #4 と #13 は同一画像を兼用
- #9 は不要と判断して削除

## 経緯: ツール選定の試行錯誤
1. Chrome headless (`--print-to-pdf`): フッター問題、ページ番号不可
2. weasyprint: Windows で GTK/Pango 依存、インストール失敗
3. fpdf2: 動作するが md パーサーを自作する必要あり、レイアウト制御が面倒
4. Python markdown + HTML テンプレート: 最も柔軟で確実 → 採用

## ルール化候補
- pip install は遠慮なく実行してよい（--break-system-packages OK）
- ユーザー向けドキュメントは PDF より HTML 1 ファイル完結が実用的（Base64 画像埋め込み、CSS/JS インライン）
