# Flipbook Material Generator

PNG連番からスプライトシートとフリップブックマテリアルを生成する VRChat 向け Unity Editor ツールです。

## 概要

`Flipbook Material Generator` は、PNG 連番画像を入力としてスプライトシートへの合成と、フリップブックアニメーション用マテリアルの生成を行う Unity Editor 拡張です。Runtime と Editor の責務を分離し、UPM 形式で配布・再利用できる構成で開発しています。

## 何ができるか

- （実装予定）PNG 連番をスプライトシートに合成する
- （実装予定）フリップブックアニメーション用マテリアルを生成する
- `Editor` と `Runtime` を分離して保守しやすい構成にする
- ドキュメント、サンプル、開発メモを同じ repo で管理する

## 導入方法

Unity プロジェクトの Package Manager から Git URL として追加します。

```
https://github.com/sebanne1225/flipbook-material-generator.git
```

またはローカルパッケージとして読み込む場合は、Package Manager の「Add package from disk...」からこの repo の `package.json` を選択します。

## 関連ファイル

- パッケージ定義: [`package.json`](./package.json)
- ライセンス: [`LICENSE`](./LICENSE)
- Runtime asmdef: [`Runtime/Sebanne.FlipbookMaterialGenerator.asmdef`](./Runtime/Sebanne.FlipbookMaterialGenerator.asmdef)
- Editor asmdef: [`Editor/Sebanne.FlipbookMaterialGenerator.Editor.asmdef`](./Editor/Sebanne.FlipbookMaterialGenerator.Editor.asmdef)
- Editor ウィンドウ: [`Editor/FlipbookMaterialGeneratorWindow.cs`](./Editor/FlipbookMaterialGeneratorWindow.cs)

## 動作確認

1. Unity プロジェクトの Package Manager から、この repo を読み込みます。
2. Unity 上部メニューの `Tools/Sebanne/Flipbook Material Generator` を開きます。
3. ウィンドウ内の `確認ログを出す` ボタンを押し、Console に 1 行ログが出ることを確認します。

## Dry Run / 診断

- 破壊的な処理を実装する前に Dry Run モードを用意し、対象件数や変更予定内容を先に確認できるようにします。
- 実処理と診断処理のログ形式をそろえ、利用者が差分を追いやすいようにします。
- 問題発生時は、対象、理由、回避策がログや UI から分かるようにします。

## 制限事項

- 現時点では具体的な機能実装は含まれていません（初期立ち上げ状態）。
- VRChat SDK 依存コードは未追加です。

## ライセンス

MIT License。詳細は `LICENSE` を参照してください。
