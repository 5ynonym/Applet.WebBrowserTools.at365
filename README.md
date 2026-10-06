# Applet.WebBrowserTools.at365

AppDock用のChromiumブラウザ操作Applet。Windows / .NET 10 / AppDock 0.6.0以降が必要です。独自UIやブラウザ拡張のインストールは不要です。

## コマンド

IDの共通接頭辞は `at365.web-browser-tools.` です。

| ID | 操作 | 送信方法／既定キー |
| --- | --- | --- |
| close-tab | タブを閉じる | Ctrl+F4 |
| back | 戻る | WM_APPCOMMAND / APPCOMMAND_BROWSER_BACKWARD (1) |
| forward | 進む | WM_APPCOMMAND / APPCOMMAND_BROWSER_FORWARD (2) |
| reload | リロード | WM_APPCOMMAND / APPCOMMAND_BROWSER_REFRESH (3) |
| super-reload | スーパーリロード | Ctrl+F5 |
| toggle-fullscreen | 全画面を切り替え | F11 |
| new-tab | 新しいタブを開く | Ctrl+T |
| new-window | 新しいウィンドウを開く | Ctrl+N |
| previous-tab | 前のタブへ | Ctrl+Shift+Tab |
| next-tab | 次のタブへ | Ctrl+Tab |
| restore-tab | 閉じたタブを復元 | Ctrl+Shift+T |

Watchのタブ操作を引き継ぎ、通常リロードとスーパーリロードを分離しています。Watchの旧ReloadはCtrl+F5です。汎用APPCOMMAND_CLOSE/NEWはタブやブラウザウィンドウを指定する契約ではないため使用しません。

## 使い方・設定

AppDockで有効にして、ホストのショートカット設定で各コマンドへ「グローバル」のキーを割り当てます。ブラウザを最前面にして実行してください。Watchのジェスチャーからも割り当てられます。AppDock自身が最前面の状態でパレットから実行すると対象外として中止します。

「設定 → Applet設定 → Applet.WebBrowserTools.at365」で、キー送信の8コマンドを個別に変更できます。保存後すぐ反映し、IDは変わりません。「送信キー」はブラウザに送るキーで、コマンドを呼び出すグローバルキーとは別です。同じキーをグローバル登録するとブラウザへ届かない場合があるため、異なる組み合わせにしてください。

記法は `Ctrl+W` / `Ctrl+Shift+T` / `Alt+Left` / `F11`。Ctrl、Alt、Shift、Winと、英数字、F1〜F24、Enter、Tab、Esc、Space、Backspace、Delete、Insert、Home、End、PageUp、PageDown、矢印キーに対応します。WinForms SendKeysの `%{F4}` のような記法は使用しません。送信自体にはWindowsのSendInputを使います。

不正な設定変更は直前の有効なコマンド設定を保持します。ただし不正値がホストに保存された状態で再起動すると有効化に失敗するので、設定欄を修正してから再起動してください。

対象の既定プロセスは `chrome, msedge, brave, vivaldi, opera, chromium, thorium`。設定でカンマ区切りのプロセス名を変更でき、`.exe`は省略可能です。対象名とChromiumのウィンドウクラスを両方確認するため、通常のElectronアプリへ誤送信しません。許可したプロセス名のChromiumウィンドウを対象とし、ブラウザのPWAも含みます。

起動操作の修飾キーが離れるまで最大2秒待ち、待機中の最前面変更で中止します。ブラウザの起動・背面ウィンドウの探索・強制フォーカス移動は行いません。メッセージが未処理またはタイムアウトした場合は失敗を報告します。管理者権限のブラウザへの操作はWindowsに制限されることがあります。

## ビルド・発行・配置

隣の `../AppDock.at365` にSDKのソースを置いて実行します。

```bat
publish.bat
deploy.bat "C:\Apps\AppDock.at365"
```

[publish.bat](publish.bat)はRelease DLLを `publish/Applet.WebBrowserTools.at365` へ発行します。任意の発行先やSDKの場所も指定できます。

```bat
publish.bat -OutputDirectory "C:\Temp\WebBrowserTools" -AppDockRoot "C:\Source\AppDock.at365"
```

[deploy.bat](deploy.bat)は標準の発行先から、指定ホストの `extensions/Applet.WebBrowserTools.at365` へDLL・deps.json・manifestを配置します。配置前に対象AppDockを終了してください。ホストのEXEと設定は変更しません。SDKはホストが提供するため配置しません。

引数を省略する場合は[deploy.local.txt.example](deploy.local.txt.example)を `deploy.local.txt` にコピーし、1行目にホストフォルダーを記入します。引数が優先され、両方未指定なら使用方法を表示して終了します。独自の発行先はdeployの入力にはならないため、配置前に通常のpublishも実行してください。

配置後にAppDockを起動し直して有効化してください。

## 検証

```powershell
dotnet run --project Applet.WebBrowserTools.RegressionTests -c Release
dotnet run --project Applet.WebBrowserTools.RegressionTests -c Release -- --native
# AppDock側のローカルNodeを使用
..\AppDock.at365\.tools\node\24.21.0\node.exe scripts\test-host.cjs ..\AppDock.at365 ..\AppDock.at365\publish\win-unpacked\AppDock.at365.exe
```

`--native`は一時的な専用ウィンドウを最前面にして入力を検証します。ホストテストは独立した `artifacts` 配下の設定だけを使います。実測と未検証事項は[VERIFICATION.md](VERIFICATION.md)を参照してください。
