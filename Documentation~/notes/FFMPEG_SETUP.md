# FFmpeg セットアップ手順

## インストール（winget 推奨）

```
winget install -e --id Gyan.FFmpeg
```

インストール後、コマンドプロンプトを **新しく開き直して** 確認：

```
ffmpeg -version
```

## PATH が通らない場合の対処

`ffmpeg` が認識されない場合、winget のインストール先にある `bin` フォルダまで PATH が通っていない可能性がある。

1. `Win + R` → `sysdm.cpl` → 詳細設定 → 環境変数
2. ユーザー変数の `Path` を編集
3. 以下のようなパスを追加（バージョン番号は実際のものに合わせる）：
   `%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-X.X.X-full_build\bin`
4. コマンドプロンプトを開き直して `ffmpeg -version` で確認

## アップデート

```
winget upgrade Gyan.FFmpeg
```

## アンインストール

```
winget uninstall Gyan.FFmpeg
```

環境変数 PATH に手動で追加した場合は、そちらも手動で削除する。
