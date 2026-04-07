# 最大シートサイズ UI 実装メモ

## やったこと

### 最大シートサイズの UI 選択を実装
- `FlipbookSheetBuilder.cs` の `private const int MaxSheetSize = 2048` を削除し、`Build` 引数で受け取る形に変更
- `FlipbookMaterialGeneratorWindow.cs` に `IntPopup`（512 / 1024 / 2048 / 4096、デフォルト 2048）を追加
- DryRun・HelpBox のマジックナンバーも `_maxSheetSize` に統一

### FlipbookPageSplitter も連動させる修正
- `CalculateFramesPerPage` に `maxSheetSize` 引数を追加
- `Split` 内のフォールバック（呼ばれない死んだコード）を削除

### 最大シートサイズ・1ページ最大フレーム数を上級設定に移動
- `EditorGUILayout.Foldout` で折りたたみエリアを追加、デフォルト閉じ

---

## 設計判断メモ

### シートサイズの影響は実は小さい
- シートサイズを変えても総テクスチャ量はほぼ変わらない
  （1枚が重くなる vs 枚数が増える のトレードオフ）
- パフォーマンスに一番効くのは **FPS** と **元 PNG の解像度 × フレーム数**
- シートサイズは「元 PNG に対して十分な器を確保するためにある」と理解するのが正確

### 上級設定に寄せた理由
- ほとんどのユーザーは 2048 固定で困らない
- 影響が小さく、触る必要がないならデフォルトで隠しておく方が UI 的にクリーン

### 最大シートサイズ vs Unity の Max Size（Import 設定）は別物

| 設定 | 何を制御するか |
|------|---------------|
| ツールの最大シートサイズ | 生成するテクスチャのピクセルサイズ上限 |
| Unity の Max Size | ビルド時に Unity が圧縮する上限。元テクスチャがそれより小さければ影響なし |
