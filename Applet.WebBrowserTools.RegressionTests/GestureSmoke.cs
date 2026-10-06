using System.Diagnostics;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Applets.WebBrowserTools;

internal static partial class NativeSmoke
{
    public static void RunGestures()
    {
        var application = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        Exception? failure = null;
        application.Startup += async (_, _) => {
            try { await GestureTests(application); }
            catch (Exception error) { failure = error; }
            finally { application.Shutdown(); }
        };
        application.Run();
        if (failure is not null) throw new Exception("Native gesture smoke failed", failure);
    }

    private static async Task GestureTests(System.Windows.Application application)
    {
        var previous = GetForegroundWindow();
        var cursor = Cursor.Position;
        var name = "Chrome_WidgetWin_WebBrowserToolsGestureFixture";
        var rightDown = 0; var rightUp = 0;
        var wheels = 0;
        var leftDown = 0; var leftUp = 0; var middleDown = 0; var middleUp = 0;
        var messages = new List<int>();
        WindowProc callback = (window, message, wParam, lParam) => {
            if (message == 0x0204) rightDown++;
            if (message == 0x0205) rightUp++;
            if (message == 0x020A) wheels++;
            if (message == 0x0201) leftDown++;
            if (message == 0x0202) leftUp++;
            if (message == 0x0207) middleDown++;
            if (message == 0x0208) middleUp++;
            if (message == 0x0319) { messages.Add(((int)lParam >> 16) & 0x7ff); return 1; }
            return DefWindowProc(window, message, wParam, lParam);
        };
        var cls = new WindowClass { Procedure = Marshal.GetFunctionPointerForDelegate(callback), Instance = GetModuleHandle(null), ClassName = name };
        if (RegisterClass(ref cls) == 0) throw new System.ComponentModel.Win32Exception();
        var window = CreateWindowEx(0, name, "WebBrowserTools gesture fixture", 0x10cf0000, 100, 100, 850, 650, 0, 0, cls.Instance, 0);
        var service = new MouseGestureService(application.Dispatcher);
        var invocations = new List<string>();
        var browsers = BrowserPlatform.ParseBrowsers(Process.GetCurrentProcess().ProcessName);
        var bindings = BrowserCommand.All.Select(c => new BrowserBinding(c, c.DefaultKeys, c.DefaultKeys is null ? null : KeyChord.Parse(c.DefaultKeys))).ToArray();
        var map = Enum.GetValues<GestureDirection>().ToDictionary(d => d, d => (BrowserBinding?)bindings.Single(b => b.Command.Id == GestureConfiguration.Defaults[(int)d]));
        var wheelMap = new Dictionary<GestureWheel, BrowserBinding?> {
            [GestureWheel.Up] = bindings.Single(b => b.Command.Id == "back"),
            [GestureWheel.Down] = bindings.Single(b => b.Command.Id == "forward")
        };
        var configuration = new GestureConfiguration(true, browsers, map, wheelMap);
        var platform = new BrowserPlatform();
        var origin = new Point(550, 450);
        var browserCentered = false;
        async Task Until(Func<bool> condition) {
            var limit = Stopwatch.StartNew();
            while (!condition()) {
                if (limit.ElapsedMilliseconds > 4000) throw new TimeoutException($"Fixture foreground={GetForegroundWindow()}, expected={window}, cursor={Cursor.Position}");
                await Task.Delay(10);
            }
        }
        void Focus(nint handle) {
            var thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var attached = thread != 0 && AttachThreadInput(GetCurrentThreadId(), thread, true);
            try { SetForegroundWindow(handle); }
            finally { if (attached) AttachThreadInput(GetCurrentThreadId(), thread, false); }
        }
        async Task Configure(GestureConfiguration value) => await service.ConfigureAsync(value, async invocation => {
            invocations.Add(invocation.Binding.Command.Id);
            await platform.ExecuteAsync(invocation.Binding.Command.AppCommand, invocation.Binding.Chord,
                invocation.Browsers, default, invocation.Target.Window);
        });
        async Task Start() { SetCursorPos(origin.X, origin.Y); await Task.Delay(30); mouse_event(8, 0, 0, 0, 0); await Task.Delay(30); }
        async Task Move(int x, int y) {
            var screen = SystemInformation.VirtualScreen;
            mouse_event(0xC001, (uint)((origin.X + x - screen.Left) * 65535L / (screen.Width - 1)),
                (uint)((origin.Y + y - screen.Top) * 65535L / (screen.Height - 1)), 0, 0);
            await Task.Delay(60);
        }
        async Task End() { mouse_event(16, 0, 0, 0, 0); await Task.Delay(220); }
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        try
        {
            Focus(window); await Until(() => GetForegroundWindow() == window);
            await Configure(configuration);
            var overlay = application.Windows.OfType<GestureIndicator>().Single();
            var shownCenters = new List<(Point Actual, Point Expected)>();
            var handle = new System.Windows.Interop.WindowInteropHelper(overlay).Handle;
            System.Windows.Interop.HwndSource.FromHwnd(handle).AddHook((nint _, int message, nint wParam, nint lParam, ref bool handled) => {
                if (message == 0x0018 && wParam != 0) {
                    GetWindowRect(handle, out var bounds);
                    GetWindowRect(window, out var browserBounds);
                    var expected = browserCentered ? new Point((browserBounds.Left + browserBounds.Right) / 2, (browserBounds.Top + browserBounds.Bottom) / 2) : origin;
                    shownCenters.Add((new((bounds.Left + bounds.Right) / 2, (bounds.Top + bounds.Bottom) / 2), expected));
                }
                return 0;
            });
            await Start(); await End(); await Until(() => rightUp == 1);
            Check(rightDown == 1 && invocations.Count == 0, "Normal right click was not replayed exactly once");

            await Start(); await Move(-70, 0);
            Check(overlay.IsVisible && GetForegroundWindow() == window,
                $"Overlay missing or stole focus: visible={overlay.IsVisible}, foreground={GetForegroundWindow()}, fixture={window}, cursor={Cursor.Position}, clicks={rightDown}/{rightUp}");
            Check(Math.Abs(overlay.Opacity - 0.45) < 0.001, "Overlay opacity did not decrease");
            var style = GetWindowLong(handle, -20);
            Check((style & (0x20 | 0x08000000 | 0x80)) == (0x20 | 0x08000000 | 0x80), "Overlay styles differ");
            var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../artifacts"));
            Directory.CreateDirectory(folder);
            GetWindowRect(handle, out var rect);
            using (var bitmap = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top)) {
                using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, bitmap.Size);
                bitmap.Save(Path.Combine(folder, "gesture-indicator.png"), ImageFormat.Png);
            }
            await End(); await Until(() => messages.Contains(1));
            Check(invocations.SequenceEqual(new[] { "back" }) && rightUp == 1 && !overlay.IsVisible, "Stroke did not execute once");

            origin = new(650, 500);
            await Start(); await Move(70, 0); await End(); await Until(() => messages.Contains(2));
            Check(shownCenters.Count >= 2 && shownCenters.All(p => Math.Abs(p.Actual.X - p.Expected.X) <= 1 && Math.Abs(p.Actual.Y - p.Expected.Y) <= 1),
                "Overlay became visible before being positioned: " + string.Join(", ", shownCenters));
            await Start(); await Move(0, 70); await End(); await Until(() => messages.Contains(3));
            await Start(); await Move(0, -70); await End();
            Check(invocations.SequenceEqual(new[] { "back", "forward", "reload", "restore-tab" }), "Four directions mismatch");
            var count = invocations.Count;
            await Start(); await Move(-70, 0); await Move(-70, 70); await Move(-140, 70); await End();
            Check(invocations.Count == count && rightUp == 1 && !overlay.IsVisible, "Turn did not permanently cancel");
            await Start(); await Move(-70, 0); await Move(0, 0); await End();
            Check(invocations.Count == count && rightUp == 1, "Reversal did not cancel");

            var unassigned = configuration with { Bindings = new Dictionary<GestureDirection, BrowserBinding?>(map) { [GestureDirection.Right] = null } };
            await Configure(unassigned); await Start(); await Move(70, 0); await End();
            Check(invocations.Count == count && rightUp == 1, "Unassigned stroke replayed a click");
            await Start(); await Move(-70, 0); await Configure(configuration with { Enabled = false }); await End();
            Check(invocations.Count == count && rightUp == 1, "Disable did not cancel pending stroke");
            await Start(); await End(); Check(rightDown == 2 && rightUp == 2, "Disabled service still captured clicks");

            await Configure(configuration); await Start(); await Move(-70, 0);
            using var other = new Form { Text = "Gesture non-browser fixture" };
            other.Show(); Focus(other.Handle); await Until(() => GetForegroundWindow() == other.Handle);
            await End(); Check(invocations.Count == count, "Foreground switch executed gesture");
            other.Hide(); Focus(window); await Until(() => GetForegroundWindow() == window);
            await Configure(configuration with { Browsers = BrowserPlatform.ParseBrowsers("unrelated") });
            await Start(); await End(); Check(rightDown == 3 && rightUp == 3, "Non-browser click was captured");
            await Configure(configuration);
            SetCursorPos(origin.X, origin.Y); await Task.Delay(30);
            for (var i = 0; i < 10; i++) { mouse_event(8, 0, 0, 0, 0); mouse_event(16, 0, 0, 0, 0); }
            await Until(() => rightUp == 13);
            Check(rightDown == 13 && invocations.Count == count, "Rapid clicks were reordered or lost");

            mouse_event(0x0800, 0, 0, 120, 0); await Until(() => wheels == 1);
            await Start();
            mouse_event(0x0800, 0, 0, 60, 0); await Task.Delay(30);
            Check(invocations.Count == count, "Partial wheel delta executed early");
            mouse_event(0x0800, 0, 0, 60, 0); await Until(() => invocations.Count == count + 1);
            mouse_event(0x0800, 0, 0, unchecked((uint)-240), 0); await Until(() => invocations.Count == count + 3);
            await Move(-70, 0); await End();
            Check(invocations.Skip(count).SequenceEqual(new[] { "back", "forward", "forward" })
                && rightUp == 13 && wheels == 1, "Wheel directions/repeats, click suppression or stroke exclusion failed");
            count = invocations.Count;
            await Start(); await Move(-70, 0); await Move(-70, 70);
            mouse_event(0x0800, 0, 0, 120, 0); await End();
            Check(invocations.Count == count, "Wheel escaped a cancelled stroke");
            await Configure(configuration with { WheelBindings = new Dictionary<GestureWheel, BrowserBinding?> {
                [GestureWheel.Up] = null, [GestureWheel.Down] = null
            } });
            await Start(); mouse_event(0x0800, 0, 0, 120, 0); await End();
            Check(invocations.Count == count && rightUp == 13 && wheels == 1, "Unassigned wheel replayed click or scrolled page");
            await Configure(configuration with { Bindings = map.ToDictionary(b => b.Key, _ => (BrowserBinding?)null) });
            await Start(); mouse_event(0x0800, 0, 0, 120, 0); await Until(() => invocations.Count == count + 1); await End();
            Check(invocations.Count == count + 1 && invocations.Last() == "back", "Wheel-only configuration did not install hook");
            count = invocations.Count;
            browserCentered = true;
            await Configure(configuration with { Placement = IndicatorPlacement.BrowserCenter, Opacity = 0.25 });
            await Start(); await Move(-70, 0);
            Check(overlay.IsVisible && Math.Abs(overlay.Opacity - 0.25) < 0.001, "Configured opacity did not apply");
            Check(shownCenters.Last().Actual == shownCenters.Last().Expected, "Browser-center overlay became visible at an incorrect position");
            await End(); count = invocations.Count; browserCentered = false;
            await Configure(configuration with { Distance = 20 });
            await Start(); await Move(-25, 0); await End();
            Check(invocations.Count == count + 1 && invocations.Last() == "back", "Configured gesture distance did not apply");
            count = invocations.Count;
            var slowStarted = 0; var slowCompleted = 0; var slowCancelled = 0;
            await service.ConfigureAsync(configuration, async invocation => {
                slowStarted++;
                try { await Task.Delay(Timeout.Infinite, invocation.Cancellation); slowCompleted++; }
                catch (OperationCanceledException) when (invocation.Cancellation.IsCancellationRequested) { slowCancelled++; }
            });
            await Start(); mouse_event(0x0800, 0, 0, 120, 0); await Until(() => slowStarted == 1);
            for (var i = 0; i < 50; i++) mouse_event(0x0800, 0, 0, 120, 0);
            await Task.Delay(250);
            Check(slowCancelled == 1, $"Idle cancellation failed: started={slowStarted}, completed={slowCompleted}, cancelled={slowCancelled}");
            Check(slowStarted == 1 && slowCompleted == 0, "Slow wheel execution built a backlog or continued after inactivity");
            mouse_event(0x0800, 0, 0, 120, 0); await Until(() => slowStarted == 2);
            await End(); await Until(() => slowCancelled == 2);
            Check(slowCompleted == 0, "Button release did not cancel slow wheel execution");
            await Configure(configuration with { WheelDelayMs = 500 });
            var beforeDelay = invocations.Count;
            await Start(); mouse_event(0x0800, 0, 0, 240, 0); await Until(() => invocations.Count == beforeDelay + 1);
            await Task.Delay(30); mouse_event(0x0800, 0, 0, unchecked((uint)-120), 0); await Task.Delay(30);
            Check(invocations.Count == beforeDelay + 1, "Wheel interval failed for a burst or reversed direction");
            await Task.Delay(520); Check(invocations.Count == beforeDelay + 1, "Rejected wheel input was executed later");
            mouse_event(0x0800, 0, 0, unchecked((uint)-120), 0); await Until(() => invocations.Count == beforeDelay + 2);
            await End(); await Start(); mouse_event(0x0800, 0, 0, 120, 0); await Until(() => invocations.Count == beforeDelay + 3);
            await End();
            var clicks = configuration with { ClickBindings = new Dictionary<GestureClick, BrowserBinding?> {
                [GestureClick.Left] = bindings.Single(b => b.Command.Id == "back"),
                [GestureClick.Middle] = bindings.Single(b => b.Command.Id == "forward")
            } };
            await Configure(clicks);
            mouse_event(2, 0, 0, 0, 0); mouse_event(4, 0, 0, 0, 0);
            mouse_event(32, 0, 0, 0, 0); mouse_event(64, 0, 0, 0, 0);
            await Until(() => leftUp == 1 && middleUp == 1);
            Check(leftDown == 1 && middleDown == 1, "Standalone left/middle clicks did not pass through");
            var beforeClicks = invocations.Count;
            await Start(); mouse_event(2, 0, 0, 0, 0); await Until(() => invocations.Count == beforeClicks + 1);
            mouse_event(4, 0, 0, 0, 0); await End();
            Check(invocations.Last() == "back" && leftDown == 1 && leftUp == 1, "Right+left click leaked to browser or executed incorrectly");
            await Start(); await Move(-70, 0); mouse_event(32, 0, 0, 0, 0); await Until(() => invocations.Count == beforeClicks + 2);
            mouse_event(64, 0, 0, 0, 0); await End();
            Check(invocations.Last() == "forward" && middleDown == 1 && middleUp == 1, "Right+middle click leaked or also executed the pending stroke");
            await Start(); mouse_event(2, 0, 0, 0, 0); await Until(() => invocations.Count == beforeClicks + 3);
            await End(); mouse_event(4, 0, 0, 0, 0); await Task.Delay(30);
            Check(leftUp == 1, "Secondary up leaked when right button was released first");
            await Start(); mouse_event(2, 0, 0, 0, 0); await Until(() => invocations.Count == beforeClicks + 4);
            await Configure(clicks with { Enabled = false }); await End(); mouse_event(4, 0, 0, 0, 0); await Task.Delay(30);
            Check(leftUp == 1, "Disabled hook lost a captured secondary up");
            mouse_event(2, 0, 0, 0, 0); mouse_event(4, 0, 0, 0, 0); await Until(() => leftUp == 2);
            Check(leftDown == 2, "Hook remained active after last secondary button release");
            var clickOnly = clicks with { Bindings = map.ToDictionary(b => b.Key, _ => (BrowserBinding?)null),
                WheelBindings = wheelMap.ToDictionary(b => b.Key, _ => (BrowserBinding?)null) };
            await Configure(clickOnly); await Start(); mouse_event(32, 0, 0, 0, 0); await Until(() => invocations.Count == beforeClicks + 5);
            mouse_event(64, 0, 0, 0, 0); await End(); Check(invocations.Last() == "forward", "Click-only configuration did not register hook");
            await Configure(clicks with { ClickBindings = clicks.ClickBindings.ToDictionary(b => b.Key, _ => (BrowserBinding?)null) });
            await Start(); mouse_event(2, 0, 0, 0, 0); mouse_event(4, 0, 0, 0, 0); await End();
            Check(invocations.Count == beforeClicks + 5 && leftUp == 2, "Unassigned click executed or leaked");
            await Configure(clicks); await Start(); await Move(-70, 0); await Move(-70, 70);
            mouse_event(32, 0, 0, 0, 0); mouse_event(64, 0, 0, 0, 0); await End();
            Check(invocations.Count == beforeClicks + 5 && middleUp == 1, "Click escaped a cancelled gesture");
            count = invocations.Count;
            await Configure(configuration); await Start(); await Move(-70, 0); await service.StopAsync(); await End();
            Check(invocations.Count == count && application.Windows.OfType<GestureIndicator>().Count() == 0, "Stop left pending action or window");
            Console.WriteLine("PASS native hook: right+left/middle clicks, standalone click pass-through, secondary-up ordering and disable cleanup, unassigned/cancelled/click-only bindings; normal/rapid right clicks, directions, wheel/no backlog/idle cancellation, display position/opacity/distance, foreground/browser gate, stop");
            Console.WriteLine(Path.Combine(folder, "gesture-indicator.png"));
        }
        finally
        {
            await service.StopAsync();
            if (window != 0) DestroyWindow(window);
            UnregisterClass(name, cls.Instance);
            SetCursorPos(cursor.X, cursor.Y);
            if (previous != 0) SetForegroundWindow(previous);
            GC.KeepAlive(callback);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, nuint extra);
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
}
