# Prefab / MA 設定

この章では、Prefab 生成と Modular Avatar（MA）連携の設定を説明します。プリセット機能を使えば、多くの場合デフォルトのままで適切な設定になります。

## プリセット

セクション上部にある **おすすめ** / **カスタム** の切り替えで、設定をまとめて管理できます。

**おすすめ** を選ぶと、Prefab 生成 ON・MA 連携すべて ON・AudioSource OFF の状態に設定されます（MultiPageSequence モードでは自動分割 ON も含まれます）。基本はこのまま使えば問題ありません。

**カスタム** を選ぶと、各設定を自由に変更できます。いずれかの設定を手動で変えると、自動で「カスタム」に切り替わります。

<!-- IMG: プリセットが「おすすめ」の状態の Prefab / MA 設定セクション全体 -->

## Prefab 生成

**Prefab も生成する** を ON にすると、Quad（フリップブックを表示する板）とマテリアルを含む Prefab が生成されます。アバターの子オブジェクトに配置するだけで使えるので、通常は ON のままにしてください。

OFF にすると、マテリアルとテクスチャだけが生成されます。自分で Quad や表示先を用意したい場合向けです。

## MA 連携（MultiPageSequence の場合）

MultiPageSequence モードでは、以下の MA 設定が表示されます。

**MA Merge Animator** は、生成された AnimatorController をアバターの FX レイヤー（VRChat がアバターの表情やギミックを制御する仕組み）に統合する設定です。VRChat アバターで使う場合は ON にしてください。OFF にすると、AnimatorController が FX に統合されないため EX メニューからの操作ができなくなります。

**MA Object Toggle** を ON にすると、VRChat の EX メニューからフリップブックの ON/OFF を切り替えられるようになります。

**トグル名** は EX メニューに表示される名前です。デフォルトは「Flipbook」ですが、自由に変更できます。**入力フォルダから取得** ボタンで入力フォルダの名前を取得することもできます。

**音源を追加** を ON にすると、音声付きのフリップブックを生成できます。詳しくは「音声つきフリップブック」の章をご覧ください。

MultiPageSequence モードの EX メニューには 3 つの操作項目が追加されます。

- **Toggle**: フリップブックの再生 ON/OFF（デフォルト OFF）
- **Loop**: ループ再生の ON/OFF（デフォルト ON）
- **Reset**: 先頭から再生し直し（ボタン型）

<!-- IMG: MPS モードで MA 設定を展開した状態（MergeAnimator / ObjectToggle / トグル名 / 音源を追加 が見える） -->

## MA 連携（Texture2DArray / LilToon の場合）

Texture2DArray と LilToon モードでは、設定項目がシンプルになります。

**MA Object Toggle を追加** を ON にすると、EX メニューからフリップブックの ON/OFF を切り替えられます。**MA Menu を追加** を ON にすると、EX メニューに項目が追加されます。

**トグル名** と **音源を追加** は MultiPageSequence と同じ使い方です。

これらのモードには AnimatorController がないため、EX メニューは Toggle（ON/OFF）のみで、Loop や Reset はありません。

## MA がなくても使える

Modular Avatar が導入されていなくても、Prefab 生成は問題なく行えます。EX メニュー連携が自動設定されないだけで、Hierarchy 上でフリップブックの GameObject を手動で ON/OFF すれば動作します。

MA は VCC から導入できます。EX メニューからの操作が必要な場合は、MA の導入をおすすめします。
