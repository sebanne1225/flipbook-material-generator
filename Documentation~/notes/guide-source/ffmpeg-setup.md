# FFmpeg セットアップ手順

動画ファイルからフリップブックを生成するには、FFmpeg というツールが必要です。この章では FFmpeg のインストール方法を説明します。

## コマンドプロンプトの開き方

FFmpeg のインストールや動作確認にはコマンドプロンプトを使います。以下のいずれかの方法で開いてください。

- Windows キーを押して「cmd」と入力 → Enter
- Win+R → 「cmd」と入力 → Enter

<!-- IMG: コマンドプロンプトで ffmpeg -version を実行した結果 -->

## インストール（winget 推奨）

コマンドプロンプトで以下のコマンドを実行します。

```
winget install -e --id Gyan.FFmpeg
```

インストールが完了したら、コマンドプロンプトを **新しく開き直して** から、以下のコマンドで正しくインストールされたか確認します。

```
ffmpeg -version
```

バージョン情報が表示されれば成功です。

## PATH が通らない場合の対処

`ffmpeg` が認識されない場合、winget のインストール先にある `bin` フォルダまで PATH が通っていない可能性があります。以下の手順で PATH を追加してください。

1. Win+R → `sysdm.cpl` → Enter で「システムのプロパティ」を開きます
2. 「詳細設定」タブ → 「環境変数」をクリックします
3. ユーザー変数の **Path** を選択して「編集」をクリックします
4. 「新規」をクリックし、以下のようなパスを追加します（バージョン番号は実際のものに合わせてください）

```
%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-X.X.X-full_build\bin
```

5. コマンドプロンプトを開き直して `ffmpeg -version` で確認します

## アップデート

FFmpeg を最新版に更新するには、以下のコマンドを実行します。

```
winget upgrade Gyan.FFmpeg
```

## アンインストール

FFmpeg を削除するには、以下のコマンドを実行します。

```
winget uninstall Gyan.FFmpeg
```

環境変数 PATH に手動で追加していた場合は、そちらも手動で削除してください。
