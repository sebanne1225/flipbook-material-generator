# 3 つの出力モード解説

## モード比較表

| 項目 | MultiPageSequence | Texture2DArray | LilToon |
|---|---|---|---|
| フレーム上限 | なし | あり（シートサイズ依存） | あり（シートサイズ依存） |
| 音声同期 | 対応 | 不可 | 不可 |
| 負荷 | やや重め（シート枚数分） | 軽量 | 軽量 |
| 必要なもの | （なし） | （なし） | lilToon シェーダー |
| EX メニュー | 再生/ループ/リセット | ON/OFF のみ | ON/OFF のみ |
| 向いてる用途 | 長尺・大量フレーム・音声付き | 短いループアニメ | lilToon 環境で手軽に |

<!-- IMG: 各モードで生成した Prefab を Scene View に並べた比較（3枚 or 1枚にまとめ） -->

## MultiPageSequence（おすすめ）

- 複数のスプライトシートに自動分割し、Animator で切り替えて再生します
- フレーム数に上限がないため、長い動画や大量のフレームも扱えます
- 音声との同期に対応しています（映像と音声がズレません）
- EX メニューから 3 つの操作ができます
  - Toggle: 再生の ON/OFF
  - Loop: ループ再生の ON/OFF（デフォルト ON）
  - Reset: 先頭から再生し直し
- テクスチャはシート枚数分読み込まれるため、他モードよりやや重めです
- → 転用元: technical/playback-control-integration.md の「3 Bool パラメータ」「MA Menu 構造」

<!-- IMG: MultiPageSequence で生成した Prefab の Hierarchy 構造（Pages/Page1〜N、Audio、MA Menu が見える） -->

## Texture2DArray

- Texture2DArray とカスタムシェーダーでフリップブック再生します
- テクスチャ 1 枚にフレームを配列として格納するため軽量です
- フレーム上限があります（UI に「最大 N フレーム ≈ X.X 秒」と表示されます）
- 音声との同期はできません（BGM や環境音など、同期不要の音声は追加可能）
- ループ再生専用です。EX メニューからは ON/OFF の切り替えのみ

<!-- IMG: Texture2DArray で生成した Prefab の Hierarchy 構造（Quad、MA Menu が見える） -->

## LilToon

- lilToon シェーダーの内蔵フリップブック機能（Main2nd DecalAnimation）を使って再生します
- lilToon が導入されている環境ならシェーダーの追加が不要で手軽です
- フレーム上限があります
- 音声との同期はできません
- ループ再生専用です
- lilToon が未導入の場合、UI に警告が表示されます

<!-- IMG: LilToon で生成した Prefab の Inspector（Material の lilToon 設定が見える） -->

## Quad の向きについて（現状の制約）

- 生成される Quad は Unity のデフォルト法線（+Z 方向）のため、Scene View のカメラ方向によっては裏面に見えることがあります
- **描画自体は問題ありません** — すべてのモードで両面描画が有効になっています
- Scene View での確認が難しい場合は、Play モードで Game View から確認してください
- 今後のアップデートで Scene View 上のガイド表示を改善予定です

## どのモードを選べばいい？

- **迷ったら MultiPageSequence** を選んでください
- 5 秒以内の短いループアニメなら → **Texture2DArray**（軽量）
- lilToon アバターでシェーダーを増やしたくないなら → **LilToon**
