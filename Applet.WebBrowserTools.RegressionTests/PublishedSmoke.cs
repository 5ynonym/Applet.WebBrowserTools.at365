using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Applets.WebBrowserTools;

internal static partial class NativeSmoke
{
    private static void RunPublished(string executable, nint window, List<int> messages, List<int> keys, Action<Task> pump)
    {
        using var child = Process.Start(new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
        }) ?? throw new Exception("Published Applet did not start");
        var errors = child.StandardError.ReadToEndAsync();
        var sequence = 0;
        async Task<JsonElement> Request(string method, object parameters, bool expectError = false)
        {
            var requestId = "fixture-" + ++sequence;
            await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = requestId, method, @params = parameters }));
            await child.StandardInput.FlushAsync();
            while (true)
            {
                var line = await child.StandardOutput.ReadLineAsync();
                if (line is null) throw new Exception("Published Applet protocol closed");
                using var document = JsonDocument.Parse(line);
                var response = document.RootElement;
                if (response.TryGetProperty("method", out var hostMethod))
                {
                    if (hostMethod.GetString() is not ("host.commands.replace" or "host.ui.panel"))
                        throw new Exception("Unexpected host request: " + hostMethod.GetString());
                    await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new {
                        jsonrpc = "2.0", id = response.GetProperty("id").GetString(), result = (object?)null
                    }));
                    await child.StandardInput.FlushAsync();
                    continue;
                }
                if (response.GetProperty("id").GetString() != requestId) throw new Exception("Response ID mismatch");
                var failed = response.TryGetProperty("error", out var error);
                if (failed != expectError) throw new Exception(failed ? error.GetRawText() : "Expected target rejection");
                return response.Clone();
            }
        }
        try
        {
            // Avoid registered global shortcuts consuming the browser's default Ctrl+Tab, etc.
            var settings = new Dictionary<string, object> {
                ["hostManagedGestures"] = true, ["browserProcesses"] = "chrome,msedge", ["requireChromiumWindowClass"] = true
            };
            var keyIndex = 13;
            foreach (var command in BrowserCommand.All.Where(c => c.DefaultKeys is not null))
                settings[command.SettingKey] = "Ctrl+Shift+F" + keyIndex++;
            var activation = Request("activate", new { id = "at365.web-browser-tools", settings });
            pump(activation);
            if (activation.Result.GetProperty("result").GetProperty("commands").GetArrayLength() != 11)
                throw new Exception("Published commands missing");
            foreach (var command in BrowserCommand.All)
            {
                var before = keys.Count;
                pump(Request("command.execute", new { id = "at365.web-browser-tools." + command.Id }));
                if (command.DefaultKeys is not null && !keys.Skip(before).Contains(KeyChord.Parse((string)settings[command.SettingKey]).Key))
                    throw new Exception("Published key not received: " + command.Id);
            }
            if (!messages.SequenceEqual(new[] { 1, 2, 3 })) throw new Exception("Published messages mismatch");
            pump(Request("command.execute", new { id = "at365.web-browser-tools.back", invocation = new {
                session = "fixture-gesture", window = window.ToString(), process = Process.GetCurrentProcess().ProcessName, source = "gesture"
            } }));
            if (messages.Count != 4) throw new Exception("Published non-browser gesture not received");
            pump(Request("command.execute", new { id = "at365.web-browser-tools.back", invocation = new {
                session = "fixture-stale", window = "1", process = "chrome", source = "gesture"
            } }, expectError: true));
            if (messages.Count != 4) throw new Exception("Published stale gesture sent input");
            settings["keys.new-tab"] = "Ctrl+Shift+F21";
            pump(Request("settings.changed", settings));
            var beforeEdit = keys.Count;
            pump(Request("command.execute", new { id = "at365.web-browser-tools.new-tab", invocation = new {
                session = "fixture-edited", window = window.ToString(), process = "not-a-browser", source = "gesture"
            } }));
            if (!keys.Skip(beforeEdit).Contains(KeyChord.Parse("Ctrl+Shift+F21").Key)
                || (GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x10) & 0x8000) != 0)
                throw new Exception("Published live edit/gesture key delivery or modifier release failed");
            pump(Request("deactivate", new { }));
            child.StandardInput.Close();
            pump(child.WaitForExitAsync());
            if (child.ExitCode != 0 || !string.IsNullOrWhiteSpace(errors.GetAwaiter().GetResult()))
                throw new Exception("Published Applet exited with errors");
            Console.WriteLine("PASS published EXE: 11 commands to non-browser Window despite browser/Chromium settings, gesture target delivery/rejection, live key edit, modifier release, clean shutdown");
        }
        finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(); } }
    }
}
