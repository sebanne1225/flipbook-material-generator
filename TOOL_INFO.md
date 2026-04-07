# TOOL_INFO

このファイルは、`Flipbook Material Generator` の repo 補助文書です。README の代わりではなく、公開準備や listing 反映時に確認したい情報を短くまとめています。

## 基本情報

- ツール名: `Flipbook Material Generator`
- package名: `com.sebanne.flipbook-material-generator`
- 表示名: `Flipbook Material Generator`
- Runtime asmdef: `Sebanne.FlipbookMaterialGenerator`
- Editor asmdef: `Sebanne.FlipbookMaterialGenerator.Editor`
- 現在 version: `1.1.0`

## 公開メタ情報

- GitHub repo: `https://github.com/sebanne1225/flipbook-material-generator`
- changelogUrl: `https://github.com/sebanne1225/flipbook-material-generator/blob/master/CHANGELOG.md`
- listing repo: `https://github.com/sebanne1225/sebanne-listing`
- 参考 listing page (`VCC` 追加先ではない): `https://sebanne1225.github.io/sebanne-listing/`
- VCC に追加する URL: `https://sebanne1225.github.io/sebanne-listing/index.json`
- listing 側に追加する `githubRepos`: `sebanne1225/flipbook-material-generator`

## 公開スコープの要約

- 動画ファイルまたは PNG 連番からスプライトシートとフリップブックマテリアルを生成する
- 3 つの出力モードに対応（MultiPageSequence / Texture2DArray / LilToon）
- Prefab 生成・MA 連携に対応（MA は optional、リフレクション方式）
- Dry Run で生成内容を事前確認できる
- 動画入力は FFmpeg 経由（FPS 指定、トリミング、音声抽出対応）

## 導入導線の前提

- 主導線は VCC / VPM
- Git URL / local package 導入は補助扱い
- MA / lilToon は optional。未導入でも基本機能は動作する
- 動画入力には FFmpeg が PATH に必要

## 既知の制限

- Texture2DArray / LilToon はフレーム数に上限あり（シートサイズに依存）
- Texture2DArray / LilToon では音声と映像の同期不可
- LilToon モードは lilToon シェーダーが必要
- lilToon プロパティ名はハードコード（バージョンアップ時に手動確認）
