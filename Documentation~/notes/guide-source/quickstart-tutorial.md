# はじめての使い方（最短チュートリアル）

動画ファイルから MultiPageSequence モードでフリップブックを作り、アバターに載せるまでの最短手順です。

## 前提

- VCC / VPM でツールが導入済みであること
- FFmpeg がインストール済みであること（動画入力の場合）
  → 参照: guide-source/ffmpeg-setup.md

## ステップ 1: ウィンドウを開く

- Unity 上部メニューの `Tools` > `Sebanne` > `Flipbook Material Generator` を選択します

<!-- IMG: Unity メニューバーから Tools > Sebanne > Flipbook Material Generator を開く様子 -->

## ステップ 2: 動画ファイルを指定する

- 「入力モード」は「動画ファイル」がデフォルトで選択されています
- 「動画ファイル」フィールドに、Assets 内の MP4 等をドラッグ＆ドロップします
- 元動画の情報（解像度・FPS・長さ）と推定フレーム数が自動表示されます
- 「書き出し FPS」はデフォルトの 8 のままで OK です（後から調整できます）

<!-- IMG: 動画ファイルを指定した直後の入力設定セクション（元動画情報と推定フレーム数が表示されている） -->

## ステップ 3: 出力モードと設定を確認する

- 「出力モード」は「MultiPageSequence」がデフォルトで選択されています
- Prefab / MA 設定のプリセットは「おすすめ」のままにします
- この状態で、Prefab 生成・MA 連携がすべて ON になっています

<!-- IMG: 出力設定セクションとPrefab/MA設定セクション（デフォルト状態） -->

## ステップ 4: Dry Run で確認する

- 実行セクションの「Dry Run」ボタンをクリックします
- ダイアログでフレーム数・ページ数・推定テクスチャメモリー増加量を確認します
- 問題なければダイアログを閉じます

<!-- IMG: Dry Run 結果ダイアログ（MultiPageSequence モード、フレーム数・ページ数・メモリー増加量が表示されている） -->

## ステップ 5: Generate で生成する

- 「Generate」ボタンをクリックします
- 生成が完了するとダイアログが表示され、生成された Prefab が Project ウィンドウでハイライトされます

<!-- IMG: Generate 完了ダイアログ -->

## ステップ 6: アバターに載せる

- 生成された Prefab をアバタールートの子オブジェクトとして配置します
- Modular Avatar が導入されていれば、EX メニューに自動追加されます
- VRChat にアップロードして動作を確認します

<!-- IMG: Hierarchy でアバタールートの子に Prefab を配置した様子 -->

## 次に読むなら

- FPS の調整について → 参照: guide-source/fps-and-length-guide.md
- 出力モードの詳しい違い → 参照: guide-source/output-modes.md
- 音声を追加したい → 参照: guide-source/audio-guide.md
