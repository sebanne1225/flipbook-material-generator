# 再生制御統合: PlaybackMode 廃止 → Enabled/Loop/Reset 3パラメータ化

## 背景

- 旧設計: PlaybackMode enum (Loop / ManualReset) で分岐。Loop と ManualReset が排他的で、ユーザーが「普段はループ再生、任意のタイミングでリセット」を両立できなかった
- AnimatorController の構造も PlaybackMode ごとに異なり、保守コストが高かった

## 設計決定

### 3 Bool パラメータ

| パラメータ | 型 | デフォルト | 用途 |
|-----------|---|-----------|------|
| FlipbookEnabled | Bool | false | 表示 ON/OFF（Idle ↔ Page 遷移） |
| FlipbookLoop | Bool | true | PageN→Page1 ループ遷移の条件 |
| FlipbookReset | Bool | false | AnyState→ResetPage1 のトリガー（Button 型: 押下中だけ true） |

### AnimatorController 構造

```
[Idle] (default) ──Enabled=true──> [Page1] → [Page2] → ... → [PageN]
  ^                                                              │
  │ AnyState(Enabled=false)                     Loop=true ──────┘
  │                                                    (exitTime=1, duration=0)
  │
  └── AnyState(Reset=true AND Enabled=true) → [ResetPage1] → [Page2]
                                                (canTransitionToSelf=true)
```

- 全遷移: duration=0, hasFixedDuration=true（即時遷移、ブレンドなし）
- ResetPage1→Page2: hasExitTime=true, exitTime=1（ResetPage1 クリップ再生完了後に遷移）
- ループ遷移の AnyState 回避: PageN→Page1 は直接遷移（AnyState 経由ではない）

### MA Menu 構造

- MultiPageSequence: SubMenu "Flipbook" → Toggle / Loop / Reset の 3 子オブジェクト
- 3モード（Texture2DArray / LilToon）: SubMenu "Flipbook" → Toggle のみ（Loop/Reset は AnimatorController がないため不要）

## Audio 制御の検証経緯

### 試行1: AudioSource.m_Enabled（不採用）

- 方針: AudioSource コンポーネントの m_Enabled を OFF→ON して playOnAwake で再起動
- 結果: **不発**。m_Enabled はコンポーネントの有効化のみ。playOnAwake は GameObject 活性化時（Awake/OnEnable）にのみ発火し、コンポーネント再有効化では発火しない
- 副次的バグ: ResetPage1 クリップの tangent 設定が C# struct コピーセマンティクスにより無効だった（`curve.keys[0].outTangent = ...` は keys プロパティが返すコピーに書き込むだけ）

### 試行2: GameObject.m_IsActive（採用）

- 方針: Audio GameObject の m_IsActive を制御
- 全 Page クリップ: m_IsActive=1（定数カーブ）→ ループ遷移で値が変わらず、Audio は再起動しない
- ResetPage1 クリップ: m_IsActive OFF→ON（stepped keyframe）→ GameObject 再活性化で playOnAwake 発火
- Idle クリップ: m_IsActive=0 → 非再生時は Audio 停止

### WD 対策

全ステート（Idle / Page1-N / ResetPage1）で Audio.m_IsActive を明示キーフレーム化。
一部ステートだけで操作すると、WD=true の他ステートでデフォルト値（false = Prefab 初期値）に書き戻される。

## 3モード vs MultiPageSequence の Audio 分岐

| 観点 | 3モード | MultiPageSequence |
|------|---------|-------------------|
| AnimatorController | なし | あり |
| Audio m_IsActive 制御 | ObjectToggle | Animation layer |
| ObjectToggle に Audio を含める | Yes | No（二重制御回避） |
| MA Menu Loop/Reset | なし | あり |

3モードの Build() は audioObj を TryAttachObjectToggleAndMenuItem に渡す → ObjectToggle に含まれる。
BuildMultiPage() は audioObj に null を渡す → ObjectToggle から除外、Animation layer が制御。

## 参照

- `session-audioSource-3mode-fixes.md`: Audio ObjectToggle 連動の旧設計（3モードでは引き続き有効）
- `2026-04-02_ffmpeg-audio-slot-browser-liltoon-fixes.md`: FFmpeg 音声抽出・スロットブラウザの設計
