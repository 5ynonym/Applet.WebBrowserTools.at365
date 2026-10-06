using System.Text.RegularExpressions;

namespace Applets.WebBrowserTools;

internal sealed record KeyChord(ushort[] Modifiers, ushort Key, bool Extended)
{
    public static KeyChord Parse(string value)
    {
        if (value.Length > 100) throw new ArgumentException("送信キーが長すぎます。");
        var parts = value.Split('+').Select(part => part.Trim().ToUpperInvariant()).ToArray();
        var modifiers = parts[..^1].Select(part => part switch {
            "CTRL" or "CONTROL" => (ushort)0x11, "ALT" => (ushort)0x12,
            "SHIFT" => (ushort)0x10, "WIN" or "WINDOWS" or "META" => (ushort)0x5B,
            _ => throw new ArgumentException($"未対応の修飾キー: {part}")
        }).ToArray();
        if (modifiers.Distinct().Count() != modifiers.Length) throw new ArgumentException("修飾キーが重複しています。");
        var name = parts[^1];
        var key = name.Length == 1 && (name[0] is >= 'A' and <= 'Z' or >= '0' and <= '9') ? (ushort)name[0]
            : Regex.IsMatch(name, "^F([1-9]|1[0-9]|2[0-4])$") ? (ushort)(0x6F + int.Parse(name[1..]))
            : name switch {
                "ENTER" => (ushort)0x0D, "TAB" => 0x09, "ESC" or "ESCAPE" => 0x1B, "SPACE" => 0x20,
                "BACKSPACE" => 0x08, "DELETE" => 0x2E, "INSERT" => 0x2D, "HOME" => 0x24, "END" => 0x23,
                "PAGEUP" => 0x21, "PAGEDOWN" => 0x22, "LEFT" => 0x25, "RIGHT" => 0x27, "UP" => 0x26, "DOWN" => 0x28,
                _ => throw new ArgumentException($"未対応の送信キー: {value}（例: Ctrl+Shift+T）")
            };
        return new(modifiers, (ushort)key, key is >= 0x21 and <= 0x28 or 0x2D or 0x2E);
    }
}
