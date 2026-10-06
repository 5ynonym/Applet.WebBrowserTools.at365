using System.Diagnostics;
using System.Runtime.InteropServices;
using Applets.WebBrowserTools;

internal static class NativeSmoke
{
    private delegate nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam);
    public static void Run()
    {
        var messages = new List<int>();
        var keys = new List<int>();
        var previous = GetForegroundWindow();
        var name = "Chrome_WidgetWin_WebBrowserToolsFixture";
        WindowProc callback = (hwnd, message, wParam, lParam) => {
            if (message == 0x0319) { messages.Add(((int)lParam >> 16) & 0x7ff); return 1; }
            if (message == 0x0100) keys.Add((int)wParam);
            return DefWindowProc(hwnd, message, wParam, lParam);
        };
        var cls = new WindowClass { Procedure = Marshal.GetFunctionPointerForDelegate(callback), Instance = GetModuleHandle(null), ClassName = name };
        if (RegisterClass(ref cls) == 0) throw new System.ComponentModel.Win32Exception();
        var hwnd = CreateWindowEx(0, name, "WebBrowserTools isolated input fixture", 0x10cf0000, 100, 100, 450, 160, 0, 0, cls.Instance, 0);
        try
        {
            if (hwnd == 0) throw new System.ComponentModel.Win32Exception();
            var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var attached = foregroundThread != 0 && AttachThreadInput(GetCurrentThreadId(), foregroundThread, true);
            try { SetForegroundWindow(hwnd); }
            finally { if (attached) AttachThreadInput(GetCurrentThreadId(), foregroundThread, false); }
            Application.DoEvents();
            if (GetForegroundWindow() != hwnd) throw new Exception("Fixture is not foreground");
            var cursor = Cursor.Position;
            var platform = new BrowserPlatform();
            var browsers = BrowserPlatform.ParseBrowsers(Process.GetCurrentProcess().ProcessName);
            void Pump(Task task) {
                var timer = Stopwatch.StartNew();
                while (!task.IsCompleted) { if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException(); Application.DoEvents(); Thread.Sleep(5); }
                task.GetAwaiter().GetResult(); Application.DoEvents();
            }
            foreach (var message in new[] { 1, 2, 3 }) Pump(platform.ExecuteAsync(message, null, browsers, default));
            Pump(platform.ExecuteAsync(null, KeyChord.Parse("Ctrl+T"), browsers, default));
            if (!messages.SequenceEqual(new[] { 1, 2, 3 }) || !keys.Contains('T') || (GetAsyncKeyState(0x11) & 0x8000) != 0)
                throw new Exception("Message or key delivery/release mismatch");
            if (Cursor.Position != cursor || GetForegroundWindow() != hwnd) throw new Exception("Cursor/focus changed");
            var pending = platform.ExecuteAsync(null, KeyChord.Parse("Ctrl+N"), browsers, default);
            using var other = new Form { Text = "Foreground switch fixture" };
            other.Show(); other.Activate(); Application.DoEvents();
            var rejected = false;
            try { Pump(pending); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected || keys.Contains('N')) throw new Exception("Foreground switch was not rejected");
            Console.WriteLine("PASS native WM_APPCOMMAND 1/2/3, SendInput Ctrl+T, modifier release, cursor/focus preservation and foreground-change cancellation");
        }
        finally {
            if (hwnd != 0) DestroyWindow(hwnd);
            UnregisterClass(name, cls.Instance);
            if (previous != 0) SetForegroundWindow(previous);
            GC.KeepAlive(callback);
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass { public uint Style; public nint Procedure; public int ClassExtra, WindowExtra; public nint Instance, Icon, Cursor, Background; public string? MenuName; public string ClassName; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClass(ref WindowClass cls);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
