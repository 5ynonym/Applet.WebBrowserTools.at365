using Applets.WebBrowserTools;

internal static partial class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--native"]) { NativeSmoke.Run(); return; }
        if (args is ["--gestures"]) { NativeSmoke.RunGestures(); return; }
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
            var ctx = new FakeContext(); var platform = new FakePlatform(); var applet = new WebBrowserToolsApplet(platform, new FakeGestures());
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
            var ctx = new FakeContext(); var platform = new FakePlatform(); var applet = new WebBrowserToolsApplet(platform, new FakeGestures());
            await applet.ActivateAsync(ctx, default); var ids = ctx.Handlers.Keys.ToArray();
            await ctx.Change("keys.close-tab", "Ctrl+W"); await ctx.Execute("close-tab"); Check(platform.Chord!.Key == 'W');
            Check(ids.SequenceEqual(ctx.Handlers.Keys));
            await Reject(() => ctx.Change("keys.close-tab", "invalid")); await ctx.Execute("close-tab"); Check(platform.Chord!.Key == 'W');
            await ctx.Change("keys.close-tab", "Ctrl+F4"); await ctx.Change("browserProcesses", "custom.exe");
            await ctx.Execute("close-tab"); Check(platform.Browsers!.SetEquals(new[] { "custom" }));
            await applet.DeactivateAsync(default);
        });
        await Test("cancellation, deactivation and restart", async () => {
            var ctx = new FakeContext(); var platform = new FakePlatform(); var applet = new WebBrowserToolsApplet(platform, new FakeGestures());
            await applet.ActivateAsync(ctx, default); var handler = ctx.Handlers[ctx.ExtensionId + ".new-tab"];
            using var cts = new CancellationTokenSource(); cts.Cancel(); await Reject(() => handler(cts.Token)); Check(platform.Calls == 0);
            await applet.DeactivateAsync(default); await handler(default); Check(platform.Calls == 0 && ctx.Changed is null);
            await applet.ActivateAsync(ctx, default); await ctx.Execute("new-tab"); Check(platform.Calls == 1);
            await applet.DeactivateAsync(default);
        });
        await Test("Watch checkpoints, single direction, reversal and permanent cancellation", () => {
            var state = new GestureState();
            foreach (var (point, direction) in new[] { (new GesturePoint(0, -51), GestureDirection.Up),
                (new GesturePoint(0, 51), GestureDirection.Down), (new GesturePoint(-51, 0), GestureDirection.Left),
                (new GesturePoint(51, 0), GestureDirection.Right) }) {
                state.Begin(new(0, 0)); Check(state.Move(point)); Check(state.Direction == direction && !state.Cancelled);
            }
            state.Begin(new(0, 0)); Check(!state.Move(new(50, 50)) && state.Direction is null);
            Check(state.Move(new(51, 0)) && state.Direction == GestureDirection.Right);
            Check(!state.Move(new(100, 20)) && !state.Cancelled);
            Check(!state.Move(new(102, 0)) && !state.Cancelled); // another checkpoint in the same direction
            Check(state.Move(new(102, 51)) && state.Cancelled);
            Check(!state.Move(new(204, 51)) && state.Cancelled);
            state.Begin(new(0, 0)); state.Move(new(0, -51)); state.Move(new(0, 0)); Check(state.Cancelled);
            state.Begin(new(0, 0), 10); Check(!state.Move(new(0, -10))); Check(state.Move(new(0, -11)) && state.Direction == GestureDirection.Up);
            Check(state.Move(new(11, -11)) && state.Cancelled);
            return Task.CompletedTask;
        });
        await Test("gesture bindings, live command keys, invalid edits and lifecycle", async () => {
            var ctx = new FakeContext(); var platform = new FakePlatform(); var gestures = new FakeGestures();
            var applet = new WebBrowserToolsApplet(platform, gestures);
            await applet.ActivateAsync(ctx, default);
            Check(gestures.Configuration!.Enabled);
            Check(gestures.Configuration.Bindings.Values.Select(b => b!.Command.Id).SequenceEqual(GestureConfiguration.Defaults));
            Check(gestures.Configuration.WheelBindings.Values.Select(b => b!.Command.Id).SequenceEqual(GestureConfiguration.WheelDefaults));
            await gestures.Invoke(GestureDirection.Left); Check(platform.Message == 1 && platform.ExpectedTarget == 123);
            var oldAction = gestures.Execute!;
            var oldInvocation = new GestureInvocation(gestures.Configuration.Bindings[GestureDirection.Left]!, new(123, "chrome.exe"), gestures.Configuration.Browsers);
            await ctx.Change("gestures.up", "new-tab"); await ctx.Change("keys.new-tab", "Ctrl+Y");
            var previousCalls = platform.Calls; await oldAction(oldInvocation); Check(platform.Calls == previousCalls);
            await gestures.Invoke(GestureDirection.Up); Check(platform.Chord!.Key == 'Y');
            await Reject(() => ctx.Change("gestures.up", "external-command"));
            await gestures.Invoke(GestureDirection.Up); Check(platform.Chord!.Key == 'Y');
            await ctx.Change("gestures.up", "none"); Check(gestures.Configuration!.Bindings[GestureDirection.Up] is null);
            await ctx.Change("gestures.wheel-up", "new-tab"); await gestures.InvokeWheel(GestureWheel.Up);
            Check(platform.Chord!.Key == 'Y');
            await Reject(() => ctx.Change("gestures.wheel-down", "external-command"));
            Check(gestures.Configuration!.WheelBindings[GestureWheel.Down]!.Command.Id == "next-tab");
            await ctx.Change("gestures.wheel-down", "none");
            Check(gestures.Configuration!.WheelBindings[GestureWheel.Down] is null);
            await ctx.Change("gestures.enabled", false); Check(!gestures.Configuration!.Enabled);
            await applet.DeactivateAsync(default); Check(gestures.Stopped);
            await applet.ActivateAsync(ctx, default); Check(!gestures.Stopped && !gestures.Configuration!.Enabled);
            await applet.DeactivateAsync(default);
        });
        await Test("gesture sends remain immediate while host hotkey reentry is suppressed", async () => {
            var ctx = new FakeContext(); var platform = new FakePlatform(); var gestures = new FakeGestures();
            var applet = new WebBrowserToolsApplet(platform, gestures);
            await applet.ActivateAsync(ctx, default);
            await gestures.InvokeWheel(GestureWheel.Up); var calls = platform.Calls;
            await ctx.Execute("previous-tab"); Check(platform.Calls == calls);
            await gestures.InvokeWheel(GestureWheel.Up); Check(platform.Calls == calls + 1);
            await Task.Delay(170); await ctx.Execute("previous-tab"); Check(platform.Calls == calls + 2);
            await applet.DeactivateAsync(default);
        });
        await Test("indicator position and opacity update live and reject invalid values", async () => {
            var ctx = new FakeContext(); var gestures = new FakeGestures();
            var applet = new WebBrowserToolsApplet(new FakePlatform(), gestures);
            await applet.ActivateAsync(ctx, default);
            Check(gestures.Configuration!.Placement == IndicatorPlacement.GestureStart && gestures.Configuration.Opacity == 0.45);
            await ctx.Change("gestures.indicator-position", "browser-center"); await ctx.Change("gestures.indicator-opacity", 0.3);
            await ctx.Change("gestures.distance", 20); Check(gestures.Configuration!.Distance == 20);
            await Reject(() => ctx.Change("gestures.distance", 0)); Check(gestures.Configuration!.Distance == 20);
            await Reject(() => ctx.Change("gestures.distance", 10.5)); Check(gestures.Configuration!.Distance == 20);
            await ctx.Change("gestures.distance", 20);
            Check(gestures.Configuration!.Placement == IndicatorPlacement.BrowserCenter && gestures.Configuration.Opacity == 0.3 && gestures.Configuration.Distance == 20);
            await Reject(() => ctx.Change("gestures.indicator-opacity", 0)); Check(gestures.Configuration!.Opacity == 0.3);
            await ctx.Change("gestures.indicator-opacity", 0.3);
            await Reject(() => ctx.Change("gestures.indicator-position", "invalid")); Check(gestures.Configuration!.Placement == IndicatorPlacement.BrowserCenter);
            await ctx.Change("gestures.indicator-position", "browser-center");
            await applet.DeactivateAsync(default); await applet.ActivateAsync(ctx, default);
            Check(gestures.Configuration!.Placement == IndicatorPlacement.BrowserCenter && gestures.Configuration.Opacity == 0.3);
            await applet.DeactivateAsync(default);
        });
        await Test("right plus left/middle click bindings update live and retain valid settings", async () => {
            var ctx = new FakeContext(); var gestures = new FakeGestures(); var platform = new FakePlatform();
            var applet = new WebBrowserToolsApplet(platform, gestures);
            await applet.ActivateAsync(ctx, default);
            Check(gestures.Configuration!.ClickBindings.Values.Select(b => b!.Command.Id).SequenceEqual(GestureConfiguration.ClickDefaults));
            await gestures.InvokeClick(GestureClick.Left); Check(platform.Chord!.Key == KeyChord.Parse("Ctrl+F4").Key && platform.ExpectedTarget == 123);
            await ctx.Change("gestures.click-middle", "back"); await gestures.InvokeClick(GestureClick.Middle); Check(platform.Message == 1);
            await Reject(() => ctx.Change("gestures.click-middle", "external-command"));
            Check(gestures.Configuration!.ClickBindings[GestureClick.Middle]!.Command.Id == "back");
            await ctx.Change("gestures.click-middle", "none"); Check(gestures.Configuration!.ClickBindings[GestureClick.Middle] is null);
            await ctx.Change("gestures.click-left", "forward");
            await applet.DeactivateAsync(default); await applet.ActivateAsync(ctx, default);
            Check(gestures.Configuration!.ClickBindings[GestureClick.Left]!.Command.Id == "forward");
            await applet.DeactivateAsync(default);
        });
        await Test("wheel delay applies between executions without reserving delayed input", () => {
            var interval = new WheelIntervalGate();
            Check(interval.TryEnter(1000, 200)); // first input is immediate
            Check(!interval.TryEnter(1001, 200) && !interval.TryEnter(1199, 200));
            Check(interval.TryEnter(1200, 200)); // rejected inputs do not extend the cooldown
            Check(!interval.TryEnter(1300, 200));
            Check(interval.TryEnter(1300, 0) && interval.TryEnter(1300, 0));
            interval.Reset(); Check(interval.TryEnter(1301, 5000));
            return Task.CompletedTask;
        });
        await Test("wheel delay setting validates, updates live and survives restart", async () => {
            var ctx = new FakeContext(); var gestures = new FakeGestures();
            var applet = new WebBrowserToolsApplet(new FakePlatform(), gestures);
            await applet.ActivateAsync(ctx, default); Check(gestures.Configuration!.WheelDelayMs == 0);
            await ctx.Change("gestures.wheel-delay-ms", 200); Check(gestures.Configuration!.WheelDelayMs == 200);
            foreach (var value in new double[] { -1, 5001, 10.5 }) {
                await Reject(() => ctx.Change("gestures.wheel-delay-ms", value));
                Check(gestures.Configuration!.WheelDelayMs == 200);
            }
            await ctx.Change("gestures.wheel-delay-ms", 200);
            await applet.DeactivateAsync(default); await applet.ActivateAsync(ctx, default);
            Check(gestures.Configuration!.WheelDelayMs == 200);
            await ctx.Change("gestures.wheel-delay-ms", 0); Check(gestures.Configuration!.WheelDelayMs == 0);
            await applet.DeactivateAsync(default);
        });
        Console.WriteLine($"{count}/{count} passed");
    }
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Assertion failed"); }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejection"); }
    private static async Task Reject(Func<Task> action) { try { await action(); } catch (Exception) { return; } throw new Exception("Expected rejection"); }
    private sealed class FakePlatform : IBrowserPlatform
    {
        public int Calls; public int? Message; public KeyChord? Chord; public IReadOnlySet<string>? Browsers; public nint ExpectedTarget;
        public Task ExecuteAsync(int? message, KeyChord? chord, IReadOnlySet<string> browsers, CancellationToken token, nint expectedTarget = 0)
        { Calls++; Message = message; Chord = chord; Browsers = browsers; ExpectedTarget = expectedTarget; return Task.CompletedTask; }
    }
    private sealed class FakeGestures : IGestureService
    {
        public GestureConfiguration? Configuration;
        public Func<GestureInvocation, Task>? Execute;
        public bool Stopped;
        public Task ConfigureAsync(GestureConfiguration configuration, Func<GestureInvocation, Task> execute)
        { Configuration = configuration; Execute = execute; Stopped = false; return Task.CompletedTask; }
        public Task StopAsync() { Stopped = true; return Task.CompletedTask; }
        public Task Invoke(GestureDirection direction) => Execute!(new(Configuration!.Bindings[direction]!, new(123, "chrome.exe"), Configuration.Browsers));
        public Task InvokeWheel(GestureWheel direction) => Execute!(new(Configuration!.WheelBindings[direction]!, new(123, "chrome.exe"), Configuration.Browsers));
        public Task InvokeClick(GestureClick button) => Execute!(new(Configuration!.ClickBindings[button]!, new(123, "chrome.exe"), Configuration.Browsers));
    }
}
