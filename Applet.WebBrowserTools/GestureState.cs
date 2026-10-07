using AppDock.SDK;

namespace Applets.WebBrowserTools;

internal enum GestureDirection { Up, Down, Left, Right }
internal enum GestureWheel { Up, Down }
internal enum GestureClick { Left, Middle }
internal enum IndicatorPlacement { GestureStart, BrowserCenter }
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal readonly record struct GesturePoint(int X, int Y);
internal sealed record BrowserTarget(nint Window, string Process);
internal sealed record BrowserBinding(BrowserCommand Command, string? Keys, KeyChord? Chord);
internal sealed record GestureInvocation(BrowserBinding Binding, BrowserTarget Target, IReadOnlySet<string> Browsers,
    CancellationToken Cancellation = default)
{
    internal bool RequireChromiumWindowClass { get; init; } = true;
}
internal sealed record GestureConfiguration(bool Enabled, IReadOnlySet<string> Browsers,
    IReadOnlyDictionary<GestureDirection, BrowserBinding?> Bindings,
    IReadOnlyDictionary<GestureWheel, BrowserBinding?> WheelBindings)
{
    internal IndicatorPlacement Placement { get; init; } = IndicatorPlacement.GestureStart;
    internal double Opacity { get; init; } = 0.45;
    internal int Distance { get; init; } = 50;
    internal int WheelDelayMs { get; init; }
    internal bool RequireChromiumWindowClass { get; init; } = true;
    internal IReadOnlyDictionary<GestureClick, BrowserBinding?> ClickBindings { get; init; } = new Dictionary<GestureClick, BrowserBinding?>();
    internal static readonly string[] Names = ["上", "下", "左", "右"];
    internal static readonly string[] Marks = ["⬆️", "⬇️", "⬅️", "➡️"];
    internal static readonly string[] Defaults = ["restore-tab", "reload", "back", "forward"];
    internal static readonly string[] WheelDefaults = ["previous-tab", "next-tab"];
    internal static readonly string[] ClickNames = ["左", "中"];
    internal static readonly string[] ClickDefaults = ["close-tab", "new-tab"];
    internal static string SettingKey(GestureDirection direction) => "gestures." + direction.ToString().ToLowerInvariant();
    internal static string SettingKey(GestureWheel direction) => "gestures.wheel-" + direction.ToString().ToLowerInvariant();
    internal static string SettingKey(GestureClick button) => "gestures.click-" + button.ToString().ToLowerInvariant();
    internal bool HasBindings => Bindings.Values.Concat(WheelBindings.Values).Concat(ClickBindings.Values).Any(b => b is not null);

    internal static GestureConfiguration Read(ISettingsService settings, IReadOnlySet<string> browsers, BrowserBinding[] commands)
    {
        var bindings = new Dictionary<GestureDirection, BrowserBinding?>();
        foreach (var direction in Enum.GetValues<GestureDirection>())
        {
            var id = settings.Get(SettingKey(direction), Defaults[(int)direction]);
            var binding = commands.SingleOrDefault(b => b.Command.Id == id);
            if (id != "none" && binding is null) throw new ArgumentException("ジェスチャーにはWebBrowserToolsのコマンドを指定してください。");
            bindings.Add(direction, binding);
        }
        var wheelBindings = new Dictionary<GestureWheel, BrowserBinding?>();
        foreach (var direction in Enum.GetValues<GestureWheel>())
        {
            var id = settings.Get(SettingKey(direction), WheelDefaults[(int)direction]);
            var binding = commands.SingleOrDefault(b => b.Command.Id == id);
            if (id != "none" && binding is null) throw new ArgumentException("ホイールにはWebBrowserToolsのコマンドを指定してください。");
            wheelBindings.Add(direction, binding);
        }
        var placement = settings.Get("gestures.indicator-position", "gesture-start") switch {
            "gesture-start" => IndicatorPlacement.GestureStart,
            "browser-center" => IndicatorPlacement.BrowserCenter,
            _ => throw new ArgumentException("待機表示の位置を確認してください。")
        };
        var opacity = settings.Get("gestures.indicator-opacity", 0.45);
        if (!double.IsFinite(opacity) || opacity < 0.1 || opacity > 1)
            throw new ArgumentException("待機表示の不透明度は0.1～1.0で指定してください。");
        var distance = settings.Get("gestures.distance", 50d);
        if (!double.IsFinite(distance) || distance < 5 || distance > 500 || distance != Math.Truncate(distance))
            throw new ArgumentException("ジェスチャーの移動距離は5～500pxの整数で指定してください。");
        var wheelDelay = settings.Get("gestures.wheel-delay-ms", 0d);
        if (!double.IsFinite(wheelDelay) || wheelDelay < 0 || wheelDelay > 5000 || wheelDelay != Math.Truncate(wheelDelay))
            throw new ArgumentException("ホイールのディレイ時間は0～5000msの整数で指定してください。");
        var clicks = new Dictionary<GestureClick, BrowserBinding?>();
        foreach (var button in Enum.GetValues<GestureClick>())
        {
            var id = settings.Get(SettingKey(button), ClickDefaults[(int)button]);
            var binding = commands.SingleOrDefault(b => b.Command.Id == id);
            if (id != "none" && binding is null) throw new ArgumentException("クリックにはWebBrowserToolsのコマンドを指定してください。");
            clicks.Add(button, binding);
        }
        return new(settings.Get("gestures.enabled", true), browsers, bindings, wheelBindings) {
            Placement = placement, Opacity = opacity, Distance = (int)distance, ClickBindings = clicks, WheelDelayMs = (int)wheelDelay
        };
    }
}

// A cooldown drops input inside the interval; it never schedules delayed work.
internal sealed class WheelIntervalGate
{
    private long lastExecution;
    private bool started;
    internal void Reset() => started = false;
    internal bool TryEnter(long now, int delayMs)
    {
        if (started && now - lastExecution < delayMs) return false;
        started = true; lastExecution = now;
        return true;
    }
}

// Watch's checkpoints and vertical-first direction ordering, with configurable distance and one stroke.
internal sealed class GestureState
{
    private GesturePoint checkpoint;
    private int distance = 50;
    internal GestureDirection? Direction { get; private set; }
    internal bool Cancelled { get; private set; }
    internal void Begin(GesturePoint point, int distance = 50) { checkpoint = point; this.distance = distance; Direction = null; Cancelled = false; }
    internal void Cancel() => Cancelled = true;
    internal bool Move(GesturePoint point)
    {
        if (Cancelled) return false;
        GestureDirection? next = point.Y < checkpoint.Y - distance ? GestureDirection.Up
            : point.Y > checkpoint.Y + distance ? GestureDirection.Down
            : point.X < checkpoint.X - distance ? GestureDirection.Left
            : point.X > checkpoint.X + distance ? GestureDirection.Right : null;
        if (next is null) return false;
        checkpoint = point;
        if (Direction is not null && Direction != next) { Cancel(); return true; }
        if (Direction == next) return false;
        Direction = next;
        return true;
    }
}

internal interface IGestureService
{
    Task ConfigureAsync(GestureConfiguration configuration, Func<GestureInvocation, Task> execute);
    Task StopAsync();
}
