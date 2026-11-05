using Avalonia;

namespace ShareVibeX.App;

internal static class Program
{
    public static string[] Arguments { get; private set; } = [];

    [STAThread]
    public static void Main(string[] args)
    {
        Arguments = args;
        AppDomain.CurrentDomain.UnhandledException += (_,e) => DiagnosticLog.Write("Unhandled: " + e.ExceptionObject);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
