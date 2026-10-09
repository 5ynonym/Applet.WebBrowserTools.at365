# 検証結果

## 2026-10-10: 開発生成物を`.artifacts`へ改名

- ユーザー指定でartifacts→.artifactsを改名。移動直後に既存1104項目の相対パス/size/mtime/ディレクトリ・リンク属性が一致し、検証終了時も元の全項目のsize/mtime/属性が不変。配布物4ファイルのSHA256も検証前後で一致。保存済みログ/JSONは内部パスを含めて保持し、過去記録の当repoのartifacts/は.artifacts/へ読み替える。
- テスト/開発用の参照とGit除外/開発手順を更新。6repo合計の変更CJS18件の構文、Gmail start-dev.ps1の構文/UTF-8 BOM、各repoのgit diff --checkが成功。旧artifactsの再生成なし、新.artifactsのGit除外を確認。
- 変更したGestureSmoke.csを含む既存.NET回帰14/14、隔離確認で開始/11コマンド登録/停止成功（.artifacts/rename-startstop-1791569716866/result.json）。GestureSmokeの実入力/画像生成は今回実行していない。
- ログは.artifacts/rename-20261010-regression.log。Gmail/Wallpaper/Watchの元の統合試験ログは.artifacts/rename-20261010-integration.log。残りの開始/停止確認の再現スクリプト/ログはA:/XX.TEMP/applets-artifacts-rename-startstop-20261010.cjsと同.log。確認スクリプトのsnapshot非同期取得/待機の途中失敗は修正し、最終は4件すべて終了0。棚卸し/最終照合はA:/XX.TEMP/applets-artifacts-rename-20261010-{before,after,final}.json。
- 製品実装は変更せず、manifest版0.3.1と既存publishを保持。再発行/commit/push/Release/実利用deployなし。同期・バックアップ設定はユキちゃんが担当。今回の成功した新規profileは各方式で直近3回以下、古い証跡は使用終了/再利用要否を一括確定していないため保持し削除0。

## 2026-10-09: v0.3.1 コマンドのブラウザー制限撤廃

- Applet内で完結。コマンド送信のプロセス一覧/Chromiumクラス判定を除去し、可視の最前面Windowへ送信する。本体管理時は通知された旧ブラウザー設定をコマンドへ適用せず、旧Appletフックも必ず停止する。11コマンドID/送信キー/初期8ジェスチャーの条件と最低本体版0.26.0は維持。AppDock本体のソース/設定は変更していない。
- Releaseビルドは警告/エラー0。回帰14/14成功（`artifacts/regression-0.3.1.log`）。本体管理で旧対象設定が不正でもコマンドを登録でき、非ブラウザーのgesture文脈の開始HWNDを保持すること、設定変更/取消/停止/再起動/再入防止と旧比較回帰を確認。
- `--native-published`終了0（`artifacts/non-browser-native-0.3.1.log`）。Chromiumクラスでもブラウザープロセスでもない専用Win32 Windowで、直接APIのWM_APPCOMMAND 1/2/3、Ctrl+T、修飾キー解放、カーソル/最前面保持、待機中の対象変更とgesture開始HWND不一致による中止を確認。ジェスチャーキー20回を49msで受信。
- 同試験で発行済み単一EXEを隔離stdin/stdout契約で起動し、browserProcesses=chrome,msedge/requireChromiumWindowClass=trueを通知した状態でも非ブラウザーWindowへ全11コマンドが届くことを確認。gestureの開始HWND一致時の送信、不一致時の未送信、キー設定の即時反映、修飾キー解放、最前面保持と終了0を確認。8送信キーはグローバル登録との競合を避けるCtrl+Shift+F13〜F20へ試験設定し、即時変更にF21を使用。既定キー対応は回帰で確認する。
- 試験途中の前面取得失敗、カーソル座標変化、既定Ctrl+Tabの未到達は成功扱いにしていない。単独再実行で前面取得と直接APIのカーソル保持は成功。Ctrl+Tab未到達の外部登録元は未特定で、既存登録との競合を避けた専用キーで送信経路を検証した。発行EXEの長い試験では外部操作と干渉しうるカーソル座標比較を分離し、前面保持と実受信を検査する。
- `publish.bat`終了0。manifest/csproj/feedは0.3.1。発行EXEは64,886,052bytes、SHA256 `9edd37f8ad40e1883eaa80cd4998b8b4ef1226c66e5347e1e63eca883297213a`。update.zipは59,398,933bytes、SHA256 `8e038c4cbb032a45c2abdfc753439a739dd8c767cf31a71fb9b07119dd9e8960`。feedの版/サイズ/hashと、ZIP内EXE/manifestの全ファイルhashを発行元と照合して一致。収録2ファイル、ID/初期割り当てはHEADと一致（`artifacts/package-0.3.1.json`）。差分検査成功。
- 本体入力フック/実ブラウザー製品/任意の実アプリがキーやメッセージをどう解釈するか、管理者権限差は今回未検証。実利用deploy/commit/push/Releaseは未実施。ユーザーのブラウザーやタブを操作していない。

## 2026-10-09: 更新配布物の自動生成

- `codex/update-packages`で発行スクリプトだけを更新。Applet本体の版は0.2.4を維持し、`publish.bat`終了コード0。共通パッカーはAppDock 0.23.0のソースから発行。
- `publish/update.json`のID・版をmanifestと照合し、ZIPのサイズ59398742bytesとSHA256 `467a43b0bd8a7787ca012a8739d010e2a3e0e44c43d029ca5a1a0b31b91e889f`を照合。ZIP内2ファイルすべてを通常発行フォルダーとバイト単位で比較し一致。収録: `Applet.WebBrowserTools.at365.exe`, `extension.json`。
- 旧SDK/旧DLLの生成物が残るWatch・WindowMover・WindowsToolsでは、既存deployと一致する配布内容へ整理する処理を追加。任意のユーザーファイルの再帰削除は行わない。
- 共通検証結果はAppDockの`artifacts/applet-update-packages.json`、発行ログは`artifacts/Applet.WebBrowserTools.at365-update-publish.log`。実利用先deploy・外部公開・pushは未実施。実GitHub/HTTP(S)/UNC配布先の確認はユーザーが後で行う。Applet固有機能・実アカウント操作の再試験は今回の発行変更の対象外。

## 2026-10-08: 実利用先へのdeploy

- 配置後の実利用について、ユーザーが正常動作を確認したと報告（2026-10-08）。

- ユーザーの明示指示により、AppDockと全6Appletの`deploy.bat`を引数なしで実行し、7件すべて終了コード0。配置先は`A:\00.ESSENTIAL\00.MainTools\AppDock.at365`。5つの.NET Appletは現ソース/SDKで`publish.bat`を先に実行し、Gmailはdeploy内で再発行した。
- AppDock0.16.2、Gmail0.5.1、WallpaperSlideshow0.3.0、Watch0.1.1（native）、WebBrowserTools0.2.4、WindowMover0.2.1、WindowsTools0.1.1を配置。Watchの古いDLL版manifestを配置せず、現ソースのnative版へ更新。
- 配置対象21ファイルのSHA256はすべて発行元と一致。現ソースと配置manifestの版/runtime/entry、minimumHostVersionも照合。settings.json・avatar.png・Gmail accounts.jsonの3ファイルは配置前後のハッシュ不変。
- 配置前後とも関連プロセスなし。実利用アプリは起動していないため、次回起動で反映する。旧ファイル退避は行わず、設定・認証領域を配置スクリプトで変更していない。結果は`../AppDock.at365/artifacts/deploy-2026-10-08-result.json`（本体では`artifacts/deploy-2026-10-08-result.json`）。

## 2026-10-08: 依存パッケージ確認

- 外部NuGet PackageReferenceなし。slnxの`dotnet list package --outdated`も更新なし。依存定義や製品コード・版の変更は不要。参照するAppDockのnpm更新詳細は[本体検証記録](../AppDock.at365/VERIFICATION.md)を参照。
- 現AppDock SDK/RuntimeでRelease build警告0/エラー0、既存RegressionTests成功。実アプリ/ハードウェアに作用するnative検証、publish/deployは今回実施していない。

## v0.2.4 EXE名のみの対象判定

2026-10-07 / Windows x64 / .NET SDK 10.0.401。

- Release回帰: 警告・エラー0、13/13成功。既定では許可したEXE名とChromiumウィンドウクラスの両方が必要なこと、切替を無効にするとEXE名のホワイトリストだけで非Chromiumのウィンドウも許可できることを確認。
- 設定変更は通常コマンドとマウスジェスチャーへ即時反映され、再有効化後も保持されることを確認。許可していないEXE名は切替を無効にしても対象外。
- `extension.json` の新しいboolean設定をJSONとして検証。実Firefoxなど非Chromiumアプリへのキー／WM_APPCOMMAND送信は未検証で、受信可否は各アプリの実装に依存する。

## v0.2.3 ホイールのディレイ設定

2026-10-07 / Windows x64 / .NET SDK 10.0.401 / AppDock製品版 win-unpacked v0.9.1。

- Releaseビルド: 警告・エラー0。回帰12/12成功。「ホイールのディレイ時間(ms)」の既定0、変更、0への復帰、範囲外/小数の拒否と直前設定保持、再起動後の保持を確認。設定範囲は0～5000msの整数。
- 間隔の回帰: 最初の入力は即実行、200ms未満の入力拒否、200msちょうどでの受付、拒否した入力が間隔を延長しないこと、0msの連続受付、リセットを確認。予約待機やタイマーによる後追い実行は追加していない。
- 実入力: 専用ウィンドウで500ms設定時に240deltaの連続刻みが1回だけ実行され、直後の逆方向入力も抑止されることを確認。待機しても拒否された入力は実行されず、間隔経過後の新しい入力と右ボタンを押し直した最初の入力は受付。既存のホイール/クリック/移動/表示設定の実入力も成功。
- AppDock GUI: 200msへ変更して保存し、パネルへ即時反映、再起動後の値保持、無効化、ホストエラー0を確認。`artifacts/host-1791327661377`。
- publish.bat: v0.2.3の自己完結単一EXEとmanifestを発行。実利用先は未配置。

ディレイはホイール上下共通の最小実行間隔で、最初の操作の遅延には使わない。実ブラウザでの操作は今回も行っていない。

## v0.2.2 右＋左/中クリック

2026-10-07 / Windows x64 / .NET SDK 10.0.401 / AppDock製品版 win-unpacked v0.9.1。

- Releaseビルド: 警告・エラー0。回帰10/10成功。左クリックの既定Ctrl+F4、左/中クリックの既定割り当て、設定変更、割り当てなし、不正な外部コマンドの拒否と直前設定保持、再起動後の設定保持を確認。
- 実入力: 専用ウィンドウで右＋左/中クリックがそれぞれ1コマンドを実行し、左/中ボタンの押下・解放をブラウザへ通さないことを確認。右ボタン解放時の移動ジェスチャー重複実行もなし。
- 解放順/無効化: 右ボタンを先に離した場合、途中でOFFにした場合も捕捉済みの左ボタン解放を消費。最後のボタン解放後はフックを解除し、通常の左クリックが再び通ることを確認。
- 範囲/取消: 単独の左/中クリックは通常操作として通過。クリックだけ有効な構成でのフック登録、割り当てなしの消費、方向転換でキャンセルした後のクリック抑止、既存の方向・ホイール・表示設定・停止の検証も成功。
- AppDock GUI: 左/中の選択欄は11コマンド＋割り当てなし。中の既定がnew-tabであること、左をリロード/中を割り当てなしへ変更して即時反映・保存・再起動保持・ホストエラー0を確認。`artifacts/host-1791327238541`。
- publish.bat: v0.2.2の自己完結単一EXEとmanifestを発行。実利用先は未配置。

実入力テスト初回は既存ホイールの再入力待ちでタイムアウトし、同条件の単独再実行では全項目成功した。実利用ブラウザでのタブ操作は今回も行っていない。

## v0.2.1 ホイール応答と表示設定

2026-10-07 / Windows x64 / .NET SDK 10.0.401 / AppDock製品版 win-unpacked v0.9.1。

- Releaseビルド: 警告・エラー0。回帰9/9成功。既存の判定・設定・ライフサイクルに加え、ジェスチャー送信によるホストコマンドの再入抑止、表示位置/不透明度/距離の即時反映・不正値拒否・再起動保持、距離10pxでの最初の方向と方向転換を確認。
- 送信速度: 専用ウィンドウで20回のCtrl+Tジェスチャー送信を44msで処理し、20回の受信を確認。ジェスチャー経由の送信後150ms待機と、キー解放済みでも発生していた送信前25ms待機を除去。ホストのグローバルキーによる再入は別の150msガードで抑止し、ジェスチャーを遅延させない。
- キュー抑止: 実マウスフックで処理待ちのコマンドへ50回の追加ホイール入力を送ったが、開始した処理は1件。入力停止後100msでの取消、右ボタン解放時の取消、続く新しい入力の受付を確認。予約/実行中の処理を1件に限定し、古い操作を後追いしない。
- 待機表示: 別の開始地点で2回表示し、WM_SHOWWINDOW受信時点の矩形中心が開始地点と一致することを確認。ブラウザ中央設定でも同じ検証に成功。表示後に位置を移す処理をなくした。
- 実表示設定: 不透明度0.45（既定）と0.25の反映、距離20px設定で25pxの移動が実行されることを確認。クリック透過・フォーカス維持・方向転換の取消も維持。最新の待機表示スクリーンショットは`artifacts/gesture-indicator.png`。
- AppDock GUI: 表示位置を「対象ブラウザーの中央」、不透明度を0.3、移動距離を20pxへ変更し、保存・パネルの即時反映・再起動保持・無効化・ホストエラー0を確認。`artifacts/host-1791326645882`。
- publish.bat: v0.2.1の自己完結単一EXEとmanifestを発行。実利用先には未配置。

実ブラウザでの処理速度と複数DPIモニターでの表示は未検証。送信済みのWindows/ブラウザ操作は取り消せない。処理が追いつかない場合の追加ホイール入力は捨てる仕様で、全刻みの後追い実行はしない。

## v0.2.0 マウスジェスチャー追加

2026-10-07 / Windows x64 / .NET SDK 10.0.401 / AppDock製品版 win-unpacked v0.9.1。

- Releaseビルド: 警告・エラー0。回帰7/7成功。既存11コマンド・安定ID・キー文法・ブラウザ制御に加え、Watchと同じ50px超のチェックポイント、同方向継続、方向転換・逆戻りによる永久キャンセル、上下左右・ホイール上下の既定割り当てを確認。
- 設定回帰: ジェスチャーとホイールの割り当て変更、送信キーとの連動、不正コマンド時の直前設定保持、保存で古い実行を取消、ON/OFF、停止・再起動を確認。
- Windows API実送信 `--native`: 専用ChromiumクラスのウィンドウでWM_APPCOMMAND 1/2/3、SendInput Ctrl+T、修飾キー解放、カーソル・フォーカス保持、待機中の最前面変更による中止に成功。
- 実マウスフック `--gestures`: 専用ウィンドウへの入力で上下左右の4コマンド、方向転換・逆戻り、通常右クリックの再送1回、10回連続クリック、割り当てなしのストローク、操作中のOFF、最前面変更、対象外プロセス、停止時のフック/表示解放を確認。
- ホイール実入力: 右ボタンを押していないスクロールの通過、上下別コマンド、60+60の部分delta積算、-240で2回実行、右ボタンを離した際のクリック・移動コマンド抑止、キャンセル後の実行抑止、割り当てなし、ホイールだけ有効な場合のフック登録を確認。
- 待機表示: WatchのXAMLを流用。500×300 DIP・角丸20・Opacity=0.7・白文字・矢印・コマンド名・プロセス名をスクリーンショットで確認。非アクティブ・クリック透過・ツールウィンドウの拡張スタイルと、最前面維持を実APIで確認。150%表示では750×450px。`artifacts/gesture-indicator.png`。
- AppDock GUI: ネイティブ単一EXEを隔離プロファイルへ配置し、11コマンド登録と6項目の選択欄（11コマンド＋割り当てなし）を確認。上下左右・ホイールの変更、キー変更の即時反映、保存、再起動後の保持、OFFの保持、無効化、ホストエラー0に成功。`artifacts/host-1791325427075`。
- publish.bat: 自己完結win-x64単一EXE＋manifestを発行。配布フォルダーに旧DLL・deps.json・SDK DLLは残らない。
- deploy.bat: 一時ホストへのEXE＋manifest配置とSHA256一致、旧DLL/deps.json除去、既存settings.jsonのSHA256不変を確認。`artifacts/gesture-deploy-a840cc37e11f4478a30d3f0d6e97d0f1`。BATのCP932往復一致・CRLFも確認。

初回のSDK参照とElectron起動はサンドボックスのアクセス制限で失敗したため、通常実行環境で再検証した。ACLやグローバルNode設定は変更していない。GUIテストは現在のdisplayNameへ追従し、入力テストと最前面が競合しないよう順番に実行した。

実利用のAppDockへの配置・各ブラウザ製品でのタブ操作・管理者権限差・複数DPIモニター間での実表示は未検証。ユーザーのブラウザやタブは操作していない。Watchのジェスチャーフックとの同時使用は検証しておらず、一方だけを有効にする運用とする。AppDock本体・他Appletのソース変更は不要だった。

## v0.1.0 既存コマンド

2026-10-06 / Windows / .NET SDK 10.0.401 / AppDock製品版 win-unpacked v0.9.0。

- Release回帰: 5/5成功。11コマンドのメッセージ／キー対応、キー文法、ブラウザ判定、設定変更、ID維持、不正設定時の直前設定保持、キャンセル・停止・再起動を確認。
- Windows API実送信: 専用テストウィンドウでWM_APPCOMMAND 1/2/3受信、SendInput Ctrl+T受信、修飾キー解放、カーソル／フォーカス保持、待機中の最前面変更による中止を確認。
- AppDock GUI: 発行済みDLLを隔離プロファイルで読み込み、11コマンド登録、送信キーをCtrl+Wへ変更して即時反映、設定ファイルへの保存、Applet再起動後の保持、無効化、ホストエラーなしを確認。`artifacts/host-1791293997894` にスクリーンショットと実行データ。
- publish.bat: 正常終了、DLL・deps.json・manifest発行。
- deploy.bat: 一時ホストフォルダーへの配置成功、3ファイルのSHA256が発行元と一致、既存settings.json不変。
- BAT: CP932のデコード／エンコード往復一致、CRLF確認。日本語のPowerShell配置スクリプトはWindows PowerShell 5.1向けにUTF-8 BOM付き。

初回のSDK参照はサンドボックスのアクセス制限で失敗し、通常実行環境で検証した。実ホストテストでパネルのActions省略が拒否されたため空配列を明示して修正。配置スクリプトのUTF-8 BOMなしによるWindows PowerShellでの読み取り失敗も修正後に再検証済み。

実利用のブラウザ・タブ・AppDock配置先は変更していない。Chrome/Edge等の各製品で実際の履歴移動・再読み込み・タブ操作が成立すること、管理者権限差、ブラウザ独自のキーカスタマイズ、グローバルキーの競合は未検証。専用ウィンドウでの検証はWindows APIの送受信を確認するもので、各ブラウザの処理を保証するものではない。

## 2026-10-09: v0.3.0 本体ジェスチャーへの移行

- AppDock 0.26.0以降を最低ホスト版とし、ジェスチャーUI/対象exe/Chromium制限を本体へ統合。11コマンドと送信キーを維持し、manifestに初回の8割り当てを宣言。hostManagedGesturesでは旧フックを有効化しない。gesture呼出しの開始HWNDとCancellationTokenを実送信まで保持し、通常経路の150ms待機を加えない。
- 回帰14/14成功。--nativeで専用WindowへのWM_APPCOMMAND 1/2/3、SendInput Ctrl+T、修飾キー解放、カーソル/前面保持、対象変更取消、20回のジェスチャーキー送信（50ms）を確認。ログは../AppDock.at365/artifacts/gestures-wbt-final.logとgestures-browser-native-final.log。
- 更新したscripts/test-host.cjsを発行済みAppDock/本Appletに実行し終了0。artifacts/host-1791535311624/result.json。旧設定の7割り当て/距離/間隔/表示移行、11コマンド、本体設定の編集/無効化、送信キーの即時反映とApplet再起動後の保持、旧設定値の保持、停止とホストエラーなしを確認。
- publish.batは本体の一括発行から終了0。manifest/csprojの版を0.3.0へ統一、通常EXE/manifest、update.json/update.zipを生成。6Applet入りZIPとの照合・起動成功。AppDockのVERIFICATIONとdocs/gestures.mdを共通証跡/契約の正本とする。
- 実利用deploy、commit、push、Releaseは未実施。実ブラウザ製品のタブ/履歴操作、権限差、物理操作は未確認。旧フックのソースは既存回帰のため残し、新ホストでは停止する。

- 追加確認: AppDock 0.26.1の共通スイッチ/パレットにhost試験を追従し、artifacts/host-1791540326790で移行/本体スイッチ/送信キー/再起動保持が成功。AppDock 0.26.2の発行でも本Applet 0.3.0を再発行・同梱し全ZIP照合/隔離起動成功。ユーザーの明示指示で発行後に移行変更をコミット。push/Release/実利用deployなし。
