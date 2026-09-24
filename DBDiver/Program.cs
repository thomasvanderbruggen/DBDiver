using Avalonia;
using DBDiver.Services;

namespace DBDiver;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LoggingService.Initialize();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            LoggingService.Shutdown();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
