# UI Restructure Notes (2026-04-04)

## やったこと
- セクション再構成: 10+セクション → 5ブロック一本道（入力設定→出力設定→Prefab/MA→実行→上級設定）
- Skinned Mesh Mirror 式 helpBox 枠 + DrawSectionHeader
- HelpBox 間引き: 説明・情報表示を DrawSubInfo (miniLabel) に降格
- ラベル日英統一: ツール概念語を日本語化（7箇所）
- 出力モード説明文追加（Info HelpBox、3モード分）
- 出力先パス表示強化（Skinned Mesh Mirror 式）
- スロット一覧をスロット選択直下に移動
- PngSequence + MPS 二重読み込み解消

## 設計判断
- HelpBox は警告・エラー専用、説明は miniLabel（UNITY_VRCHAT_TOOL_UI_GUIDELINES.md 準拠）
- Dry Run / Generate は英語のまま残す判断
- Texture2DArray clamp=64 と CalculateMaxFrames のズレは安全側なので保留
- FPS 計算機の折りたたみ化は後回し
