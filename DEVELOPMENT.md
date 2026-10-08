# Applet.WebBrowserTools.at365 開発ガイド

利用方法は[README.md](README.md)、実測結果と未確認事項は[VERIFICATION.md](VERIFICATION.md)を参照してください。

## ビルド・発行・配置

開発には.NET SDK 10と、隣の `../AppDock.at365` にSDK/Runtimeのソースが必要です。

```bat
publish.bat
deploy.bat "C:\Apps\AppDock.at365"
```

[publish.bat](publish.bat)はReleaseの自己完結単一EXEとmanifestを `publish/Applet.WebBrowserTools.at365` へ発行します。任意の発行先やSDKの場所も指定できます。

```bat
publish.bat -OutputDirectory "C:\Temp\WebBrowserTools" -AppDockRoot "C:\Source\AppDock.at365"
```

[deploy.bat](deploy.bat)は標準の発行先から、指定ホストの `extensions/Applet.WebBrowserTools.at365` へEXE・manifestを配置し、旧版のDLL・deps.jsonを除去します。配置前に対象AppDockを終了してください。ホストのEXEと設定は変更しません。SDK/RuntimeはAppletのEXEに含まれます。既存のコマンドIDと送信キー設定は引き続き使用できます。

引数を省略する場合は[deploy.local.txt.example](deploy.local.txt.example)を `deploy.local.txt` にコピーし、1行目にホストフォルダーを記入します。引数が優先され、両方未指定なら使用方法を表示して終了します。独自の発行先はdeployの入力にはならないため、配置前に通常のpublishも実行してください。

配置後にAppDockを起動し直して有効化してください。

## 検証

```powershell
dotnet run --project Applet.WebBrowserTools.RegressionTests -c Release
dotnet run --project Applet.WebBrowserTools.RegressionTests -c Release -- --native
dotnet run --project Applet.WebBrowserTools.RegressionTests -c Release -- --gestures
# AppDock側のローカルNodeを使用
..\AppDock.at365\.tools\node\24.21.0\node.exe scripts\test-host.cjs ..\AppDock.at365 ..\AppDock.at365\publish\win-unpacked\AppDock.at365.exe
```

`--native`と`--gestures`は一時的な専用ウィンドウを最前面にして入力を検証します。後者はカーソルを動かして終了時に位置を戻し、待機表示を`artifacts/gesture-indicator.png`へ保存します。GUI/入力テストは最前面が競合しないよう1本ずつ実行してください。ホストテストは独立した `artifacts` 配下の設定だけを使います。実測と未検証事項は[VERIFICATION.md](VERIFICATION.md)を参照してください。

## 操作の実装

Watchのタブ操作を引き継ぎ、通常リロードとスーパーリロードを分離しています。Watchの旧ReloadはCtrl+F5です。汎用APPCOMMAND_CLOSE/NEWはタブやブラウザウィンドウを指定する契約ではないため使用しません。

戻る・進む・リロードはWM_APPCOMMANDのAPPCOMMAND_BROWSER_BACKWARD (1)、APPCOMMAND_BROWSER_FORWARD (2)、APPCOMMAND_BROWSER_REFRESH (3)を使い、キー送信はWindowsのSendInputを使用します。既定では対象プロセス名とChromiumのウィンドウクラスを両方検査します。「Chromiumウィンドウのみを対象にする」を無効にした場合は、プロセス名のホワイトリストだけで判定します。待機表示はWatchのXAMLを引き継いでいます。

## 文書の更新

READMEには動作環境・導入・操作・設定・利用上の制約を記載します。開発環境・ビルド・テスト・発行・開発者用配置・実装の説明はこのファイル、実測結果と未検証事項はVERIFICATION.mdへ記載します。共通方針は[AppDockのドキュメント方針](../AppDock.at365/docs/documentation.md)を参照してください。

## 更新配布物の発行

`publish.bat`は通常の発行先を生成した後、兄弟のAppDockリポジトリにある`scripts/pack-applet-update.ps1`で`publish/update.json`と`publish/update.zip`を自動生成します。共通パッカーのビルドに.NET 10 SDKが必要です。Gmail以外のAppletは、このパッケージ生成のためにNode.jsを導入する必要はありません。

ZIP直下に`extension.json`と実行ファイル一式を置き、JSONにID・版・必要な本体版・ZIPのサイズとSHA256を記録します。`OutputDirectory`を指定できる発行スクリプトでも、指定先の配布内容を読み、更新用JSON/ZIPの出力先はこのリポジトリの`publish`です。通常配置用サブフォルダーへJSON/ZIPを混ぜず、`deploy.bat`の配置対象も増やしません。

Web配布やGitHub Releaseには同じ発行で生成したJSONとZIPを一緒に置き、JSONを最後に公開してください。ソースコードの自動生成ZIPは使用しません。発行スクリプトから外部公開は行いません。[共通更新仕様](../AppDock.at365/docs/updates.md)と[配布先の確認手順](../AppDock.at365/docs/update-checklist.md)を参照してください。
