using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Applets.WebBrowserTools;

internal interface IBrowserPlatform
{
    Task ExecuteAsync(int? appCommand, KeyChord? chord, IReadOnlySet<string> browsers, CancellationToken token,
        nint expectedTarget = 0, bool requireChromiumWindowClass = true);
}

internal sealed class BrowserPlatform : IBrowserPlatform
{
    public const string DefaultBrowsers = "chrome, msedge, brave, vivaldi, opera, chromium, thorium";
    private static readonly int[] HeldModifiers = [0x10, 0x11, 0x12, 0x5B, 0x5C];
    internal static HashSet<string> ParseBrowsers(string value)
    {
        var names = value.Split(',', StringSplitOptions.TrimEntries);
        if (names.Length > 32 || names.Any(n => !Regex.IsMatch(n, @"^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,79}$")))
            throw new ArgumentException("ブラウザのプロセス名をカンマ区切りで指定してください（例: chrome, msedge）。");
        return names.Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    internal static bool IsBrowser(string process, string windowClass, IReadOnlySet<string> browsers,
        bool requireChromiumWindowClass = true) =>
        browsers.Contains(process) && (!requireChromiumWindowClass
            || windowClass.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal));

    public async Task ExecuteAsync(int? appCommand, KeyChord? chord, IReadOnlySet<string> browsers, CancellationToken token,
        nint expectedTarget = 0, bool requireChromiumWindowClass = true)
    {
        token.ThrowIfCancellationRequested();
        var target = Target(browsers, requireChromiumWindowClass);
        if (expectedTarget != 0 && target != expectedTarget)
            throw new InvalidOperationException("ジェスチャーを開始したブラウザが最前面ではないため送信を中止しました。");
        if (appCommand is int command)
        {
            // Chromium checks current modifier state for browser commands as well.
            await WaitForRelease(target, null, token);
            if (SendMessageTimeout(target, 0x0319, target, new nint(command << 16), 0x0002 | 0x0020, 1000, out var handled) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ブラウザへメッセージを送信できませんでした。");
            if (handled == 0) throw new InvalidOperationException("ブラウザが操作メッセージを処理しませんでした。");
        }
        else await SendAsync(target, chord ?? throw new ArgumentNullException(nameof(chord)), token, expectedTarget == 0);
    }
    private static async Task WaitForRelease(nint target, KeyChord? chord, CancellationToken token)
    {
        var started = Environment.TickCount64;
        while (HeldModifiers.Any(Pressed) || (chord is not null && Pressed(chord.Key)))
        {
            await Task.Delay(25, token);
            if (GetForegroundWindow() != target || !IsWindowVisible(target))
                throw new InvalidOperationException("最前面のウィンドウが変わったため送信を中止しました。");
            if (Environment.TickCount64 - started >= 2000)
                throw new InvalidOperationException("修飾キーを離してから再実行してください。");
        }
        token.ThrowIfCancellationRequested();
        if (!IsCurrentTarget(target))
            throw new InvalidOperationException("最前面のウィンドウが変わったため送信を中止しました。");
    }
    private static bool Pressed(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    private static nint Target(IReadOnlySet<string> browsers, bool requireChromiumWindowClass)
        => ForegroundTarget(browsers, requireChromiumWindowClass).Window;

    internal static BrowserTarget? TryGestureTarget(IReadOnlySet<string> browsers, bool requireChromiumWindowClass,
        GesturePoint point)
    {
        try
        {
            var target = ForegroundTarget(browsers, requireChromiumWindowClass);
            return IsPointOnTarget(target.Window, point) ? target : null;
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or Win32Exception) { return null; }
    }
    internal static bool IsPointOnTarget(nint target, GesturePoint point) =>
        GetAncestor(WindowFromPoint(point), 2) == target;
    internal static bool IsCurrentTarget(nint target) => GetForegroundWindow() == target && IsWindowVisible(target);
    internal static GesturePoint WindowCenter(nint target)
    {
        if (!GetWindowRect(target, out var rect)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
    }
    [StructLayout(LayoutKind.Sequential)] private struct WindowRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(nint window, out WindowRect rect);

    private static BrowserTarget ForegroundTarget(IReadOnlySet<string> browsers, bool requireChromiumWindowClass)
    {
        var window = GetForegroundWindow();
        if (window == 0 || !IsWindowVisible(window)) throw new InvalidOperationException("ブラウザを最前面にしてください。");
        GetWindowThreadProcessId(window, out var pid);
        using var process = Process.GetProcessById((int)pid);
        var name = new StringBuilder(256);
        if (GetClassName(window, name, name.Capacity) == 0
            || !IsBrowser(process.ProcessName, name.ToString(), browsers, requireChromiumWindowClass))
        {
            var target = requireChromiumWindowClass ? "対象のChromiumブラウザ" : "許可したプロセス名のアプリ";
            throw new InvalidOperationException($"{target}を最前面にしてください。対象プロセス名はApplet設定で変更できます。");
        }
        return new(window, process.ProcessName + ".exe");
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessageTimeout(nint window, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nuint result);
    private static async Task SendAsync(nint target, KeyChord chord, CancellationToken token, bool guardHostCommand)
    {
        await WaitForRelease(target, chord, token);
        var events = new List<Input>();
        foreach (var modifier in chord.Modifiers) events.Add(KeyEvent(modifier, false, modifier == 0x5B));
        events.Add(KeyEvent(chord.Key, false, chord.Extended));
        events.Add(KeyEvent(chord.Key, true, chord.Extended));
        foreach (var modifier in chord.Modifiers.Reverse()) events.Add(KeyEvent(modifier, true, modifier == 0x5B));
        var sent = SendInput((uint)events.Count, events.ToArray(), Marshal.SizeOf<Input>());
        if (sent != events.Count)
        {
            var error = Marshal.GetLastWin32Error();
            // Only release keys for which we may have injected key-down successfully.
            var releases = events.Take((int)sent).Where(e => (e.Data.Keyboard.Flags & 2) == 0)
                .Reverse().Select(e => KeyEvent(e.Data.Keyboard.Key, true, (e.Data.Keyboard.Flags & 1) != 0)).ToArray();
            if (releases.Length > 0) SendInput((uint)releases.Length, releases, Marshal.SizeOf<Input>());
            throw new InvalidOperationException($"キーを送信できませんでした（{sent}/{events.Count}, Win32={error}）。管理者権限のアプリには送信が制限されます。");
        }
        // Keep the command in flight while Windows dispatches any matching global hotkey.
        if (guardHostCommand) await Task.Delay(150, token);
    }
    private static Input KeyEvent(ushort key, bool up, bool extended) => new() {
        Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = key, Flags = (up ? 2u : 0) | (extended ? 1u : 0) } }
    };
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(GesturePoint point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
