# Flipbook Material Generator

動画ファイルまたは PNG 連番から、VRChat 向けのフリップブックアニメーション素材（スプライトシート・マテリアル・Prefab）を自動生成する Unity Editor ツールです。

用途に合わせた 3 つの出力モードに対応し、Modular Avatar（MA）連携や音声同期にも対応しています。

## 何ができるか

- 動画ファイル（MP4 等）または PNG 連番フォルダからスプライトシートとマテリアルを自動生成できます
- 3 つの出力モードに対応しています（MultiPageSequence / Texture2DArray / LilToon）
- Prefab 生成・MA 連携に対応しています（MA は optional、未導入でも Prefab 生成は可能）
- Dry Run で生成内容を事前に確認できます
- 元アバターには直接変更を加えない非破壊設計です

## 出力モード

### MultiPageSequence（おすすめ）

PNG 連番を複数のスプライトシートに自動分割し、AnimatorController でシームレスにループ再生します。フレーム数の制限がなく、音声との同期も可能です。EX メニューから再生・ループ・リセットを制御できます。

### Texture2DArray

Texture2DArray とカスタムシェーダーでフリップブック再生します。軽量ですが、フレーム数に上限があります（シートサイズに依存）。ループ再生専用です。

### LilToon

lilToon シェーダーの内蔵フリップブック機能を使って再生します。lilToon 環境ならシェーダー追加なしで使えます。フレーム数に上限があり、ループ再生専用です。

## 対応環境

- Unity `2022.3`
- VRChat SDK（Avatars）
- FFmpeg（動画入力を使う場合。`winget install ffmpeg` 等でインストール）
- VCC / VPM ベースの VRChat プロジェクトを推奨します

### 推奨

- Modular Avatar（Prefab の自動メニュー連携に必要。未導入でも Prefab 生成は可能）
- lilToon（LilToon モードを使う場合）

## VCC / VPM での導入

### 推奨: VCC / VPM から導入

1. VCC に追加する URL として `https://sebanne1225.github.io/sebanne-listing/index.json` を追加します。
2. package 一覧から `Flipbook Material Generator` (`com.sebanne.flipbook-material-generator`) を追加します。
3. Unity を開き、package が導入されていることを確認します。

参考ページ (`VCC` 追加先ではありません): `https://sebanne1225.github.io/sebanne-listing/`

### 補助: Git URL / Release zip から導入

- repo: `https://github.com/sebanne1225/flipbook-material-generator`
- Git URL や local package での導入は、開発確認や手動検証向けの補助導線です
- GitHub Release の zip も補助導線として使えます。`com.sebanne.flipbook-material-generator-<version>.zip` を展開すると、直下に `package.json` が見える package 構成です

## 使い方

1. Unity 上部メニューの `Tools/Sebanne/Flipbook Material Generator` を開きます。
2. 入力設定で動画ファイルまたは PNG 連番フォルダを指��します。
3. FPS を設定します（動画入力の場合はトリミングも可能）。
4. 出力モードを選択します（迷ったら MultiPageSequence）。
5. まず `Dry Run` で生成内容を確認します。
6. 問題なければ `Generate` で生成します。

### 動画入力の場合

- FFmpeg が必要です（PATH が通っていることを確認してください）
- FPS・トリミング（開始時間 / 長さ）を指定できます
- 動画からの音声自動抽出にも対応しています

### Prefab / MA 連携

- 「Prefab も生成する」を ON にすると、Quad + マテリアルを含む Prefab が生成されます
- MA が導入されていれば、EX メニュー連携（ObjectToggle / MenuInstaller）を自動設定できます
- 音源を追加すると、AudioSource 付きの子オブジェクトが生成されます

## Dry Run

- `Dry Run` では実ファイルを生成せず、生成予定の内容をダイアログで確認できます
- 出力モード、フレーム数、ページ構成、生成ファイル数、MA 設定などが表示されます
- 入力や設定に問題がある場合は警告が表示されます
- `Generate` の前にまず `Dry Run` で確認する運用を推奨します

## 出力先

出力先は 3 つから選べます。

- **元ソース直下**: 入力フォルダ直下に `Generated_Flipbook/` を作成します
- **ツール共通フォルダ**（デフォルト）: `Assets/Sebanne/FlipbookMaterialGenerator/Generated_Flipbook/` に生成します
- **フォルダを指定**: 任意のフォルダを選択できます

出力はスロット方式（`Generated_Flipbook/01_名前/`）で管理され、同名スロットへの上書きやスロット削除に対応しています。

## Release Asset

GitHub Release には、VPM 配布確認や手動保管に使える package zip を添付します。

- 例: `com.sebanne.flipbook-material-generator-<version>.zip`

## 制限事項

- Texture2DArray / LilToon モードはフレーム数に上限があります（シートサイズから自動計算、UI に表示されます）
- Texture2DArray / LilToon モードでは音声と映像の同期はできません（ループ BGM 等の用途に限られます）
- LilToon モードは lilToon シェーダーが必要です
- 動画入力には FFmpeg のインストールが必要です

## ライセンス

MIT License です。詳細は `LICENSE` を参照してください。
