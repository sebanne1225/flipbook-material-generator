# Prefab / MA 設定

Prefab / MA 設定セクションでは、Prefab 生成と Modular Avatar（MA）連携の設定を行います。

## プリセット

- UI ラベル: 「おすすめ」 / 「カスタム」（Toolbar）
- **おすすめ**: Prefab ON / MA 全設定 ON / AudioSource OFF の状態に設定されます（MPS では自動分割 ON も含む）
- **カスタム**: 各設定を自由に変更できます
- いずれかの設定を手動で変えると、自動で「カスタム」に切り替わります
- 基本は「おすすめ」のまま使えば問題ありません

<!-- IMG: プリセットが「おすすめ」の状態の Prefab / MA 設定セクション全体 -->

## Prefab 生成

- UI ラベル: 「Prefab も生成する」
- **ON**: Quad + マテリアルを含む Prefab が生成されます。アバターに配置するだけで使えます
- **OFF**: マテリアルとテクスチャだけ生成されます。自分で Quad を用意する場合向けです

## MA 連携（MultiPageSequence の場合）

MultiPageSequence モードでは MA の詳細設定ができます。

- UI ラベル: 「MA Merge Animator」
  - AnimatorController をアバターの FX レイヤーに統合します
  - VRChat アバターで使う場合は ON にしてください（OFF にすると警告が表示されます）

- UI ラベル: 「MA Object Toggle」
  - EX メニューからフリップブックの ON/OFF を切り替えられるようにします

- UI ラベル: 「トグル名」
  - EX メニューに表示される名前です。デフォルトは「Flipbook」
  - 「入力フォルダから取得」ボタンで入力フォルダ名を取得できます

- UI ラベル: 「音源を追加」
  - 音声付きフリップブックを作成します → 参照: guide-source/audio-guide.md

- EX メニューの構造: Flipbook > Toggle（ON/OFF）/ Loop（ループ切替）/ Reset（先頭に戻す）
  → 転用元: technical/playback-control-integration.md の「MA Menu 構造」

<!-- IMG: MPS モードで MA 設定を展開した状態（MergeAnimator / ObjectToggle / トグル名 / 音源を追加 が見える） -->

## MA 連携（Texture2DArray / LilToon の場合）

Texture2DArray / LilToon モードでは、設定項目が少しシンプルになります。

- UI ラベル: 「MA Object Toggle を追加」
  - EX メニューから ON/OFF を切り替えられるようにします

- UI ラベル: 「MA Menu を追加」
  - EX メニューに項目を追加します（ObjectToggle を ON にすると表示されます）

- UI ラベル: 「トグル名」「音源を追加」は MPS と同じです

- EX メニューの構造: Flipbook > Toggle（ON/OFF のみ。Loop / Reset はありません）

## MA がなくても使える

- Modular Avatar が導入されていなくても、Prefab 生成は可能です
- EX メニュー連携がなくなるだけで、Hierarchy 上で手動で ON/OFF すれば動きます
- MA の導入は VCC から行えます
