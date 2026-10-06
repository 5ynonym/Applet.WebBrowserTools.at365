using AppDock.SDK;

namespace Applets.WebBrowserTools;

public sealed class WebBrowserToolsApplet : IAppDockExtension
{
    private readonly IBrowserPlatform platform;
    private readonly SemaphoreSlim gate = new(1, 1);
    private IExtensionContext context = null!;
    private IDisposable? subscription;
    private bool active;
    public WebBrowserToolsApplet() : this(new BrowserPlatform()) { }
    internal WebBrowserToolsApplet(IBrowserPlatform platform) => this.platform = platform;

    public async Task ActivateAsync(IExtensionContext context, CancellationToken token)
    {
        this.context = context;
        active = true;
        await Refresh(token);
        subscription = context.Settings.OnChanged(Refresh);
    }

    private async Task Refresh(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (!active) return;
            var browsers = BrowserPlatform.ParseBrowsers(context.Settings.Get("browserProcesses", BrowserPlatform.DefaultBrowsers));
            // Validate every key before replacing any handler. Invalid edits retain the last working set.
            var bindings = BrowserCommand.All.Select(command => (Command: command,
                Keys: command.DefaultKeys is null ? null : context.Settings.Get(command.SettingKey, command.DefaultKeys)))
                .Select(binding => (binding.Command, binding.Keys, Chord: binding.Keys is null ? null : KeyChord.Parse(binding.Keys))).ToArray();
            await context.Commands.ReplaceAsync(bindings.Select(binding => new CommandRegistration(
                context.ExtensionId + "." + binding.Command.Id, binding.Command.Title, async ct => {
                    await gate.WaitAsync(ct);
                    try {
                        ct.ThrowIfCancellationRequested();
                        if (active) await platform.ExecuteAsync(binding.Command.AppCommand, binding.Chord, browsers, ct);
                    }
                    finally { gate.Release(); }
                })).ToArray(), token);
            await context.Ui.ShowPanelAsync(new Panel("WebBrowserTools",
                "ブラウザを最前面にして、AppDockのグローバルショートカットやWatchのジェスチャーから実行してください。送信キーの変更は保存後すぐ反映されます。",
                bindings.Select(b => new PanelFact(b.Command.Title, b.Keys ?? $"WM_APPCOMMAND ({b.Command.AppCommand})")).ToArray(), []), token);
        }
        finally { gate.Release(); }
    }

    public async Task DeactivateAsync(CancellationToken token)
    {
        subscription?.Dispose(); subscription = null;
        await gate.WaitAsync(token);
        try { active = false; }
        finally { gate.Release(); }
    }
}
