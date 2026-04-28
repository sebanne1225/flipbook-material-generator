# 2026-04-05 リリース準備セッション

## やったこと
- AudioClip 未指定警告追加（HelpBox Warning）
- AudioClip loadInBackground = true（SDK Auto Fix 解消）
- _generatePrefab デフォルト true
- README / TOOL_INFO / CHANGELOG / package.json 全面整備
- release workflow 新規（avatar-audio-safety-guard ベース）
- BOOTH_PACKAGE 4点作成
- URL整合確認（問題0件）
- public-release-sync-check（Blocking 0）
- version 1.0.0 固定 → commit → push → GitHub Release → VPM → VCC 確認

## 発見事項
- default branch は master（main ではない）
- README に文字化け発生（「指定」→「指◆◆」）— push 後に発覚、修正済み
- public-release-sync-check Skill は PROJECT_SHARED_CONTEXT.md を要求するが knowledge repo のファイル名が CLAUDE.md で不一致。shared_context_text は読むだけで使っていないため、optional 化が最善

## 後回し候補（新規）
- 容量見積もり表示
- Quad 裏表ダミーオブジェクト
- public-release-sync-check Skill の修正（PROJECT_SHARED_CONTEXT.md 不一致、TOOL_INFO パーサー、REPO_INDEX パーサー）
