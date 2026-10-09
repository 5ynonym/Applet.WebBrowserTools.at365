# WebBrowserToolsの作業ルール

最初に[A:の共通指示](../../AGENTS.md)、[30.PROJECT共通指示](../AGENTS.md)、[AppDockの指示](../AppDock.at365/AGENTS.md)を読む。開発の入口は[DEVELOPMENT.md](DEVELOPMENT.md)、共通契約は[Applet API](../AppDock.at365/docs/extensions.md)、[マウスジェスチャー](../AppDock.at365/docs/gestures.md)。

- 本体管理のジェスチャーを使い、Applet側フックを二重起動しない。コマンドID/送信キーを維持し、対象exeとChromium制限は本体から渡される値を使う。
- gesture呼出しの開始HWND/取消情報を実送信まで保持する。再入防止と連続操作の速さを両立し、通常経路の150ms待機をジェスチャーへ流用しない。
- 完成した変更はmanifest/プロジェクトの版を揃えpublish.batで発行し、回帰/隔離実入力を検証する。実利用deploy/commit/push/公開は別の依頼。