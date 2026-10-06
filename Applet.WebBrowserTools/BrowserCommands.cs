namespace Applets.WebBrowserTools;

internal sealed record BrowserCommand(string Id, string Title, int? AppCommand = null, string? DefaultKeys = null)
{
    public static readonly BrowserCommand[] All = [
        new("close-tab", "タブを閉じる", DefaultKeys: "Ctrl+F4"),
        new("back", "戻る", 1),
        new("forward", "進む", 2),
        new("reload", "リロード", 3),
        new("super-reload", "スーパーリロード", DefaultKeys: "Ctrl+F5"),
        new("toggle-fullscreen", "全画面を切り替え", DefaultKeys: "F11"),
        new("new-tab", "新しいタブを開く", DefaultKeys: "Ctrl+T"),
        new("new-window", "新しいウィンドウを開く", DefaultKeys: "Ctrl+N"),
        new("previous-tab", "前のタブへ", DefaultKeys: "Ctrl+Shift+Tab"),
        new("next-tab", "次のタブへ", DefaultKeys: "Ctrl+Tab"),
        new("restore-tab", "閉じたタブを復元", DefaultKeys: "Ctrl+Shift+T")
    ];
    public string SettingKey => "keys." + Id;
}
