# DeleteAsset → File.Delete 置換
日付: 2026-04-04

## 経緯
AssetDatabase.DeleteAsset は内部的に OS のゴミ箱送り。
Generate / スロット削除を繰り返すとゴミ箱に PNG + .meta が大量蓄積。

## 対応
- FlipbookFileUtility.cs を新規追加（DeleteFileAndMeta / DeleteFolderAndMeta）
- 全9箇所（4ファイル）を置換
- #4 AnimatorBuilder, #7 スロット削除に Refresh 新規追加
- #8-9 ClearSlotContents に一括 Refresh 追加

## 知見
- AssetDatabase.DeleteAsset = ゴミ箱送り（Unity 仕様）
- File.Delete + .meta の File.Delete + AssetDatabase.Refresh で完全削除できる
- CreateAsset / CreateAnimatorControllerAtPath は AssetDatabase 上に「ない」状態が前提なので、File.Delete 後に Refresh で同期してから呼ぶ
