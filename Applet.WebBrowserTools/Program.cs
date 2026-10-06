using System.Text;
using System.Windows;
using AppDock.Runtime;

namespace Applets.WebBrowserTools;

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        var protocol = Console.Out;
        Console.SetOut(Console.Error);
        if (!Console.IsInputRedirected) return 2;
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var applet = new WebBrowserToolsApplet(new BrowserPlatform(), new MouseGestureService(application.Dispatcher));
        application.DispatcherUnhandledException += (_, e) => {
            Console.Error.WriteLine(e.Exception); e.Handled = true; application.Shutdown(1);
        };
        _ = Task.Run(async () => {
            var code = 0;
            try { await AppletSession.RunAsync(applet, Console.In, protocol); }
            catch (Exception e) { Console.Error.WriteLine(e); code = 1; }
            finally { await application.Dispatcher.InvokeAsync(() => application.Shutdown(code)); }
        });
        return application.Run();
    }
}
