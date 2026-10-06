using Applets.WebBrowserTools;

internal static partial class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--native"]) { NativeSmoke.Run(); return; }
        Run().GetAwaiter().GetResult();
    }
    private static async Task Run()
    {
        var count = 0;
        async Task Test(string name, Func<Task> action) { await action(); Console.WriteLine("PASS " + name); count++; }
        await Test("key grammar and invalid keys", () => {
            Check(KeyChord.Parse("Ctrl+Shift+T").Modifiers.SequenceEqual(new ushort[] { 0x11, 0x10 }));
            Check(KeyChord.Parse("Alt+Left").Extended);
            foreach (var value in new[] { "", "Ctrl+", "Ctrl+Control+A", "F25", "%{F4}", "Ctrl+A+B" })
                Throws(() => KeyChord.Parse(value));
            return Task.CompletedTask;
        });
        await Test("browser gate excludes Electron, unrelated applications and non-browser windows", () => {
            var browsers = BrowserPlatform.ParseBrowsers("chrome.exe, MSEDGE");
            Check(BrowserPlatform.IsBrowser("chrome", "Chrome_WidgetWin_1", browsers));
            Check(BrowserPlatform.IsBrowser("msedge", "Chrome_WidgetWin_1", browsers));
            Check(!BrowserPlatform.IsBrowser("AppDock.at365", "Chrome_WidgetWin_1", browsers));
            Check(!BrowserPlatform.IsBrowser("chrome", "Notepad", browsers));
            foreach (var value in new[] { "", "chrome,", "C:\\chrome.exe", "../chrome", "chrome*" }) Throws(() => BrowserPlatform.ParseBrowsers(value));
            return Task.CompletedTask;
        });
        await Test("all eleven commands route to expected messages and Watch keys", async () => {
            var ctx = new FakeContext(); var platform = new FakePlatform(); var applet = new WebBrowserToolsApplet(platform);
            await applet.ActivateAsync(ctx, default); Check(ctx.Handlers.Count == 11);
            foreach (var (id, message) in new[] { ("back", 1), ("forward", 2), ("reload", 3) }) {
                await ctx.Execute(id); Check(platform.Message == message && platform.Chord is null);
            }
            foreach (var (id, key) in new[] { ("close-tab", "Ctrl+F4"), ("super-reload", "Ctrl+F5"), ("toggle-fullscreen", "F11"),
                ("new-tab", "Ctrl+T"), ("new-window", "Ctrl+N"), ("previous-tab", "Ctrl+Shift+Tab"), ("next-tab", "Ctrl+Tab"), ("restore-tab", "Ctrl+Shift+T") }) {
                await ctx.Execute(id); var expected = KeyChord.Parse(key);
                Check(platform.Message is null && platform.Chord!.Key == expected.Key && platform.Chord.Modifiers.SequenceEqual(expected.Modifiers));
            }
            await applet.DeactivateAsync(default);
        });
        await Test("live key edits preserve IDs and invalid edits retain working bindings", async () => {
            var ctx = new FakeContext(); var platform = new FakePlatform(); var applet = new WebBrowserToolsApplet(platform);
            await applet.ActivateAsync(ctx, default); var ids = ctx.Handlers.Keys.ToArray();
            await ctx.Change("keys.close-tab", "Ctrl+W"); await ctx.Execute("close-tab"); Check(platform.Chord!.Key == 'W');
            Check(ids.SequenceEqual(ctx.Handlers.Keys));
            await Reject(() => ctx.Change("keys.close-tab", "invalid")); await ctx.Execute("close-tab"); Check(platform.Chord!.Key == 'W');
            await ctx.Change("keys.close-tab", "Ctrl+F4"); await ctx.Change("browserProcesses", "custom.exe");
            await ctx.Execute("close-tab"); Check(platform.Browsers!.SetEquals(new[] { "custom" }));
            await applet.DeactivateAsync(default);
        });
        await Test("cancellation, deactivation and restart", async () => {
            var ctx = new FakeContext(); var platform = new FakePlatform(); var applet = new WebBrowserToolsApplet(platform);
            await applet.ActivateAsync(ctx, default); var handler = ctx.Handlers[ctx.ExtensionId + ".new-tab"];
            using var cts = new CancellationTokenSource(); cts.Cancel(); await Reject(() => handler(cts.Token)); Check(platform.Calls == 0);
            await applet.DeactivateAsync(default); await handler(default); Check(platform.Calls == 0 && ctx.Changed is null);
            await applet.ActivateAsync(ctx, default); await ctx.Execute("new-tab"); Check(platform.Calls == 1);
            await applet.DeactivateAsync(default);
        });
        Console.WriteLine($"{count}/{count} passed");
    }
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Assertion failed"); }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejection"); }
    private static async Task Reject(Func<Task> action) { try { await action(); } catch (Exception) { return; } throw new Exception("Expected rejection"); }
    private sealed class FakePlatform : IBrowserPlatform
    {
        public int Calls; public int? Message; public KeyChord? Chord; public IReadOnlySet<string>? Browsers;
        public Task ExecuteAsync(int? message, KeyChord? chord, IReadOnlySet<string> browsers, CancellationToken token)
        { Calls++; Message = message; Chord = chord; Browsers = browsers; return Task.CompletedTask; }
    }
}
