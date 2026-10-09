# WebBrowserToolsの作業ルール

最初に[A:の共通指示](../../AGENTS.md)、[30.PROJECT共通指示](../AGENTS.md)、[AppDockの指示](../AppDock.at365/AGENTS.md)を読む。開発の入口は[DEVELOPMENT.md](DEVELOPMENT.md)、共通契約は[Applet API](../AppDock.at365/docs/extensions.md)、[マウスジェスチャー](../AppDock.at365/docs/gestures.md)。

- 本体管理のジェスチャーを使い、Applet側フックを二重起動しない。コマンドID/送信キーを維持する。v0.3.1以降、コマンドはアクティブなウィンドウへ送信し、プロセス名/Chromiumクラスで拒否しない。実行場所は本体の割り当て条件で決める。本体が通知するbrowserProcesses/requireChromiumWindowClassをコマンド送信へ適用しない。
- 旧GestureState/MouseGestureServiceの対象ブラウザ判定は比較回帰用に残すが、hostManagedGesturesでは必ず無効化する。新しい実行制限やApplet側フックを追加しない。
- gesture呼出しの開始HWND/取消情報を実送信まで保持する。再入防止と連続操作の速さを両立し、通常経路の150ms待機をジェスチャーへ流用しない。
- 完成した変更はmanifest/プロジェクトの版を揃えpublish.batで発行し、回帰/隔離実入力を検証する。実利用deploy/commit/push/公開は別の依頼。
- 開発/試験生成物の保存先は`.artifacts`。作業完了時の整理はAppDockのDEVELOPMENT.mdの共通手順に従う。旧ログの内部パスは履歴値として保持する。
