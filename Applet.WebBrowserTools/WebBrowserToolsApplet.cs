using AppDock.SDK;

namespace Applets.WebBrowserTools;

public sealed class WebBrowserToolsApplet : IAppDockExtension
{
    private readonly IBrowserPlatform platform;
    private readonly IGestureService gestures;
    private readonly SemaphoreSlim gate = new(1, 1);
    private IExtensionContext context = null!;
    private IDisposable? subscription;
    private bool active;
    private CancellationTokenSource? gestureLifetime;
    private long gestureKeyGuardUntil;
    public WebBrowserToolsApplet() : this(new BrowserPlatform(), new MouseGestureService(System.Windows.Threading.Dispatcher.CurrentDispatcher)) { }
    internal WebBrowserToolsApplet(IBrowserPlatform platform, IGestureService gestures)
    { this.platform = platform; this.gestures = gestures; }

    public async Task ActivateAsync(IExtensionContext context, CancellationToken token)
    {
        this.context = context;
        active = true;
        try
        {
            await Refresh(token);
            subscription = context.Settings.OnChanged(Refresh);
        }
        catch
        {
            active = false; gestureLifetime?.Cancel(); gestureLifetime?.Dispose(); gestureLifetime = null;
            await gestures.StopAsync(); throw;
        }
    }

    private async Task Refresh(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (!active) return;
            var hostManagedGestures = context.Settings.Get("hostManagedGestures", false);
            // The host owns invocation conditions. These settings belong only to the legacy hook.
            var browsers = BrowserPlatform.ParseBrowsers(hostManagedGestures ? BrowserPlatform.DefaultBrowsers
                : context.Settings.Get("browserProcesses", BrowserPlatform.DefaultBrowsers));
            var requireChromiumWindowClass = !hostManagedGestures && context.Settings.Get("requireChromiumWindowClass", true);
            // Validate every key before replacing any handler. Invalid edits retain the last working set.
            var bindings = BrowserCommand.All.Select(command => (Command: command,
                Keys: command.DefaultKeys is null ? null : context.Settings.Get(command.SettingKey, command.DefaultKeys)))
                .Select(binding => new BrowserBinding(binding.Command, binding.Keys, binding.Keys is null ? null : KeyChord.Parse(binding.Keys))).ToArray();
            var configuration = GestureConfiguration.Read(context.Settings, browsers, bindings) with {
                Enabled = !hostManagedGestures && context.Settings.Get("gestures.enabled", true),
                RequireChromiumWindowClass = requireChromiumWindowClass
            };
            var nextLifetime = new CancellationTokenSource();
            var nextToken = nextLifetime.Token;
            try { await gestures.ConfigureAsync(configuration, invocation => ExecuteGesture(invocation, nextToken)); }
            catch { nextLifetime.Dispose(); throw; }
            gestureLifetime?.Cancel(); gestureLifetime?.Dispose();
            gestureLifetime = nextLifetime;
            await context.Commands.ReplaceAsync(bindings.Select(binding => new CommandRegistration(
                context.ExtensionId + "." + binding.Command.Id, binding.Command.Title, async ct => {
                    var hostGesture = CommandExecution.Current is { Source: "gesture" } invocation ? invocation : null;
                    // SendInput can dispatch a configured global hotkey back to this Applet.
                    // Guard that return path without delaying mouse gestures.
                    if (hostGesture is null && Environment.TickCount64 < Interlocked.Read(ref gestureKeyGuardUntil)) return;
                    await gate.WaitAsync(ct);
                    try {
                        ct.ThrowIfCancellationRequested();
                        if (hostGesture is null && Environment.TickCount64 < Interlocked.Read(ref gestureKeyGuardUntil)) return;
                        if (hostGesture is not null && binding.Chord is not null)
                            Interlocked.Exchange(ref gestureKeyGuardUntil, Environment.TickCount64 + 150);
                        if (active) await platform.ExecuteAsync(binding.Command.AppCommand, binding.Chord, ct,
                            expectedTarget: hostGesture is null ? 0 : new nint(long.Parse(hostGesture.Window)));
                        if (hostGesture is not null && binding.Chord is not null)
                            Interlocked.Exchange(ref gestureKeyGuardUntil, Environment.TickCount64 + 150);
                    }
                    finally { gate.Release(); }
                })).ToArray(), token);
            if (hostManagedGestures) {
                await context.Ui.ShowPanelAsync(new Panel("WebBrowserTools",
                    "ブラウザ以外でも使える便利コマンド集です。アクティブなウィンドウへ送信します。実行する場所はAppDock本体のショートカット・マウスジェスチャーの割り当て条件で設定してください。",
                    bindings.Select(b => new PanelFact(b.Command.Title, b.Keys ?? $"WM_APPCOMMAND ({b.Command.AppCommand})")).ToArray(), []), token);
                return;
            }
            await context.Ui.ShowPanelAsync(new Panel("WebBrowserTools",
                "ブラウザ以外でも使える便利コマンド集です。アクティブなウィンドウへ送信します。実行する場所はAppDock本体の割り当て条件で設定してください。送信キーの変更は保存後すぐ反映されます。",
                bindings.Select(b => new PanelFact(b.Command.Title, b.Keys ?? $"WM_APPCOMMAND ({b.Command.AppCommand})"))
                    .Concat([new PanelFact("マウスジェスチャー", configuration.Enabled ? "有効（右ボタン）" : "無効")])
                    .Concat([new PanelFact("Chromiumウィンドウの確認", configuration.RequireChromiumWindowClass ? "有効" : "EXE名のみ")])
                    .Concat([new PanelFact("待機表示の位置", configuration.Placement == IndicatorPlacement.BrowserCenter ? "対象ブラウザーの中央" : "ジェスチャー開始地点"),
                        new PanelFact("待機表示の不透明度", Math.Round(configuration.Opacity * 100) + "%")])
                    .Concat([new PanelFact("ジェスチャーの移動距離", configuration.Distance + "px")])
                    .Concat([new PanelFact("ホイールのディレイ時間", configuration.WheelDelayMs + "ms")])
                    .Concat(configuration.Bindings.Select(b => new PanelFact("ジェスチャー：" + GestureConfiguration.Names[(int)b.Key], b.Value?.Command.Title ?? "割り当てなし")))
                    .Concat(configuration.WheelBindings.Select(b => new PanelFact("ホイール：" + GestureConfiguration.Names[(int)b.Key], b.Value?.Command.Title ?? "割り当てなし")))
                    .Concat(configuration.ClickBindings.Select(b => new PanelFact("クリック：" + GestureConfiguration.ClickNames[(int)b.Key], b.Value?.Command.Title ?? "割り当てなし")))
                    .ToArray(), []), token);
        }
        finally { gate.Release(); }
    }

    private async Task ExecuteGesture(GestureInvocation invocation, CancellationToken token)
    {
        if (!active) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, invocation.Cancellation);
        token = linked.Token;
        try
        {
            await gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                if (active)
                {
                    if (invocation.Binding.Chord is not null)
                        Interlocked.Exchange(ref gestureKeyGuardUntil, Environment.TickCount64 + 150);
                    await platform.ExecuteAsync(invocation.Binding.Command.AppCommand, invocation.Binding.Chord,
                        token, invocation.Target.Window);
                    if (invocation.Binding.Chord is not null)
                        Interlocked.Exchange(ref gestureKeyGuardUntil, Environment.TickCount64 + 150);
                }
            }
            finally { gate.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { await context.Log.ErrorAsync("マウスジェスチャー: " + error.Message); }
    }

    public async Task DeactivateAsync(CancellationToken token)
    {
        subscription?.Dispose(); subscription = null;
        gestureLifetime?.Cancel();
        await gate.WaitAsync(token);
        try { active = false; await gestures.StopAsync(); gestureLifetime?.Dispose(); gestureLifetime = null; gestureKeyGuardUntil = 0; }
        finally { gate.Release(); }
    }
}
