using Serilog;

namespace DBDiver.Services;

public static class LoggingService
{
    public static void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("logs/dbdiver-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    public static void Shutdown()
    {
        Log.CloseAndFlush();
    }
}
