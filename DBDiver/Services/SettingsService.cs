using System.Text.Json;
using System.Text.Json.Serialization;
using DBDiver.Models;
using Serilog;

namespace DBDiver.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Load()
    {
        var filePath = GetSettingsPath();
        try
        {
            if (!File.Exists(filePath))
                return new AppSettings();

            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings from {FilePath}", filePath);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var filePath = GetSettingsPath();
        try
        {
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings, JsonOptions));
            Log.Information("Saved settings to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {FilePath}", filePath);
        }
    }

    private static string GetSettingsPath() => Path.Combine(AppContext.BaseDirectory, "settings.json");
}
