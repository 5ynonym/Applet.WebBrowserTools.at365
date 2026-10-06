using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Applets.WebBrowserTools;

internal sealed class MouseGestureService(Dispatcher dispatcher) : IGestureService
{
    internal const nuint InputMarker = 0x365B012;
    private readonly GestureState state = new();
    private readonly WheelIntervalGate wheelInterval = new();
    private GestureConfiguration? configuration;
    private Func<GestureInvocation, Task>? execute;
    private GestureIndicator? indicator;
    private DispatcherTimer? timer;
    private HookCallback? callback;
    private nint hook;
    private BrowserTarget? target;
    private GesturePoint origin;
    private bool ready, stopped;
    private bool wheelMode;
    private bool clickMode, clickScheduled, clickInFlight;
    private readonly HashSet<GestureClick> capturedClicks = [];
    private CancellationTokenSource? clickLifetime;
    private int wheelDelta;
    private CancellationTokenSource? wheelLifetime;
    private bool wheelInFlight;
    private bool wheelScheduled;
    private long lastWheelInput;
    private const int WheelInputExpiryMilliseconds = 100;
    private int revision;

    public Task ConfigureAsync(GestureConfiguration settings, Func<GestureInvocation, Task> action) =>
        dispatcher.InvokeAsync(() => {
            stopped = false;
            // Create the native hook before replacing the last working configuration.
            if (settings.Enabled && settings.HasBindings && hook == 0)
            {
                indicator ??= new GestureIndicator();
                callback ??= Hook;
                hook = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0);
                if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "マウスジェスチャーのフックを登録できませんでした。");
            }
            revision++;
            wheelInterval.Reset();
            CancelWheel();
            CancelClick();
            if (ready) state.Cancel();
            indicator?.Hide();
            configuration = settings; execute = action;
            if (!ready && !Enabled && capturedClicks.Count == 0) Unhook();
        }).Task;

    private bool Enabled => configuration is { Enabled: true, HasBindings: true };

    public Task StopAsync() => dispatcher.InvokeAsync(() => {
        stopped = true; revision++; ready = false; target = null;
        CancelWheel();
        CancelClick(); capturedClicks.Clear();
        timer?.Stop(); state.Cancel(); Unhook();
        indicator?.Close(); indicator = null;
        configuration = null; execute = null;
    }).Task;

    private void Unhook()
    {
        if (hook == 0) return;
        if (!UnhookWindowsHookEx(hook)) throw new Win32Exception(Marshal.GetLastWin32Error());
        hook = 0;
    }

    private nint Hook(int code, uint message, nint data)
    {
        if (code < 0 || stopped) return CallNextHookEx(hook, code, message, data);
        var mouse = Marshal.PtrToStructure<HookData>(data);
        if (mouse.Extra == InputMarker) return CallNextHookEx(hook, code, message, data);
        try
        {
            // Consume each matching up even if right-button-up or settings changes came first.
            var releasedClick = message == 0x0202 ? GestureClick.Left : message == 0x0208 ? GestureClick.Middle : (GestureClick?)null;
            if (releasedClick is { } button && capturedClicks.Remove(button))
            {
                if (!ready && !Enabled && capturedClicks.Count == 0) Unhook();
                return 1;
            }
            if (message == 0x0204 && !ready && Enabled)
            {
                target = BrowserPlatform.TryGestureTarget(configuration!.Browsers, mouse.Point);
                if (target is not null)
                {
                    origin = mouse.Point; state.Begin(origin, configuration!.Distance); ready = true; wheelMode = false; clickMode = false; wheelDelta = 0;
                    CancelWheel();
                    wheelInterval.Reset();
                    CancelClick();
                    timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Normal,
                        (_, _) => UpdateIndicator(), dispatcher);
                    timer.Start();
                    return 1;
                }
            }
            else if (ready)
            {
                if (!BrowserPlatform.IsCurrentTarget(target!.Window)) state.Cancel();
                if (!wheelMode && !clickMode && message is (0x0200 or 0x0205)) state.Move(mouse.Point);
                if (message is 0x0201 or 0x0207)
                {
                    var click = message == 0x0201 ? GestureClick.Left : GestureClick.Middle;
                    if (capturedClicks.Add(click)) Click(click);
                    return 1;
                }
                if (message == 0x020A) { Wheel(unchecked((short)(mouse.Data >> 16))); return 1; }
                if (message == 0x0205) { Release(mouse.Point); return 1; }
                if (message == 0x0204) return 1;
            }
        }
        catch (Exception error)
        {
            // Do not let exceptions cross the unmanaged hook boundary.
            state.Cancel(); Console.Error.WriteLine(error);
            if (ready && message == 0x0205) { ready = false; timer?.Stop(); indicator?.Hide(); return 1; }
        }
        return CallNextHookEx(hook, code, message, data);
    }

    private void UpdateIndicator()
    {
        if (!ready) return;
        if (!BrowserPlatform.IsCurrentTarget(target!.Window)) state.Cancel();
        if (state.Cancelled) CancelClick();
        if (state.Cancelled || (wheelMode && Environment.TickCount64 - lastWheelInput >= WheelInputExpiryMilliseconds)) CancelWheel();
        if (!wheelMode && !clickMode && GetCursorPos(out var point)) state.Move(point);
        if (wheelMode || clickMode || state.Cancelled || state.Direction is null) { indicator?.Hide(); return; }
        var direction = state.Direction.Value;
        if (indicator!.IsVisible) return; // one stroke has only one caption; avoid relayout on every tick
        var settings = configuration!;
        var center = settings.Placement == IndicatorPlacement.BrowserCenter ? BrowserPlatform.WindowCenter(target.Window) : origin;
        indicator.Display(center, target.Process, direction,
            settings.Bindings[direction]?.Command.Title ?? "アクション無し", settings.Opacity);
    }

    private void Release(GesturePoint point)
    {
        var capturedTarget = target!;
        var capturedRevision = revision;
        var binding = !wheelMode && !clickMode && !state.Cancelled && state.Direction is { } direction ? configuration!.Bindings[direction] : null;
        var replay = !wheelMode && !clickMode && !state.Cancelled && state.Direction is null;
        var invocation = binding is null ? null : new GestureInvocation(binding, capturedTarget, configuration!.Browsers);
        var action = execute;
        CancelWheel();
        CancelClick();
        ready = false; target = null; timer?.Stop();
        // Actions and replay run after returning from the hook, as in Watch.
        dispatcher.BeginInvoke(new Action(async () => {
            indicator?.Hide();
            if (stopped || capturedRevision != revision || !BrowserPlatform.IsCurrentTarget(capturedTarget.Window)) return;
            try
            {
                if (invocation is not null) await action!(invocation);
                else if (replay && BrowserPlatform.IsPointOnTarget(capturedTarget.Window, point)
                    && GetCursorPos(out var cursor) && BrowserPlatform.IsPointOnTarget(capturedTarget.Window, cursor)) ReplayClick();
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
        }));
        if (!Enabled && capturedClicks.Count == 0) Unhook();
    }

    private void Click(GestureClick button)
    {
        if (state.Cancelled) return;
        clickMode = true;
        CancelWheel();
        var binding = configuration!.ClickBindings.GetValueOrDefault(button);
        if (binding is null || clickScheduled || clickInFlight) return;
        clickLifetime ??= new CancellationTokenSource();
        var clickToken = clickLifetime.Token;
        var invocation = new GestureInvocation(binding, target!, configuration.Browsers, clickToken);
        var capturedRevision = revision;
        var action = execute;
        clickScheduled = true;
        dispatcher.BeginInvoke(new Action(async () => {
            clickScheduled = false;
            if (clickToken.IsCancellationRequested || stopped || capturedRevision != revision
                || !BrowserPlatform.IsCurrentTarget(invocation.Target.Window)) return;
            clickInFlight = true;
            try { await action!(invocation); }
            catch (OperationCanceledException) when (clickToken.IsCancellationRequested) { }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { clickInFlight = false; }
        }));
    }

    private void CancelClick()
    {
        clickLifetime?.Cancel(); clickLifetime?.Dispose(); clickLifetime = null;
    }

    private void Wheel(int delta)
    {
        if (state.Cancelled || delta == 0) return;
        wheelMode = true;
        lastWheelInput = Environment.TickCount64;
        wheelDelta += delta;
        var repeats = Math.Abs(wheelDelta) / 120;
        if (repeats == 0) return;
        var direction = wheelDelta > 0 ? GestureWheel.Up : GestureWheel.Down;
        wheelDelta %= 120;
        var binding = configuration!.WheelBindings[direction];
        if (binding is null || wheelInFlight || wheelScheduled) return;
        wheelLifetime ??= new CancellationTokenSource();
        var wheelToken = wheelLifetime.Token;
        var invocation = new GestureInvocation(binding, target!, configuration.Browsers, wheelToken);
        var capturedRevision = revision;
        var capturedTime = lastWheelInput;
        var delayMs = configuration.WheelDelayMs;
        var action = execute;
        wheelScheduled = true;
        dispatcher.BeginInvoke(new Action(async () => {
            wheelScheduled = false;
            // No wheel backlog: one operation at a time, with expired/released input discarded.
            if (wheelInFlight || wheelToken.IsCancellationRequested || stopped || capturedRevision != revision
                || Environment.TickCount64 - capturedTime >= WheelInputExpiryMilliseconds
                || !BrowserPlatform.IsCurrentTarget(invocation.Target.Window)) return;
            wheelInFlight = true;
            try
            {
                for (var i = 0; i < repeats; i++)
                {
                    if (wheelToken.IsCancellationRequested || Environment.TickCount64 - lastWheelInput >= WheelInputExpiryMilliseconds
                        || !BrowserPlatform.IsCurrentTarget(invocation.Target.Window)) break;
                    if (!wheelInterval.TryEnter(Environment.TickCount64, delayMs)) break;
                    await action!(invocation);
                }
            }
            catch (OperationCanceledException) when (wheelToken.IsCancellationRequested) { }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { wheelInFlight = false; }
        }));
    }

    private void CancelWheel()
    {
        wheelLifetime?.Cancel(); wheelLifetime?.Dispose(); wheelLifetime = null;
    }

    private static void ReplayClick()
    {
        var events = new[] { MouseEvent(0x0008), MouseEvent(0x0010) };
        var sent = SendInput(2, events, Marshal.SizeOf<Input>());
        if (sent != 2)
        {
            var error = Marshal.GetLastWin32Error();
            if (sent == 1) SendInput(1, [MouseEvent(0x0010)], Marshal.SizeOf<Input>());
            throw new Win32Exception(error, "通常の右クリックを再送信できませんでした。");
        }
    }
    private static Input MouseEvent(uint flags) => new() { Data = new InputUnion { Mouse = new MouseInput { Flags = flags, Extra = InputMarker } } };
    private delegate nint HookCallback(int code, uint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct HookData { public GesturePoint Point; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int kind, HookCallback callback, nint module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, uint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out GesturePoint point);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
