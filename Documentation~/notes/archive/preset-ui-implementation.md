# プリセット UI 実装メモ

## 実施内容
- MAMode（Simple / Advanced）を廃止し、プリセット UI に置き換え
- プリセット: おすすめ / カスタム（途中から再開は未実装、将来拡張）
- 個別設定（再生モード・MA 各種）はプリセット後に上書き可能
- 手動変更時は自動で「カスタム」に切り替わる

## 設計決定
- enableMenu = false 時は ObjectToggle + MA Menu 構造を丸ごとスキップ
  MergeAnimator のみアタッチ、Pages/ は生成されるが表示制御なし
- enableMenu = true 時の ObjectToggle は MA Menu/ 配下の Toggle オブジェクトに配置（変更前と同じ）

## フィールド変更
- _maMode / MAMode enum → 廃止
- _useMergeAnimator → _enableMergeAnimator
- _addObjectToggle → _enableObjectToggle
- 追加: _enableMenu, _preset（FlipbookPreset enum）

## 動作確認
- 動作確認済み
