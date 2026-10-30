namespace DBDiver.Models;

public class AppSettings
{
    public bool HasSeenWelcomeGuide { get; set; }
    public string ThemeName { get; set; } = Services.ColorSchemeService.DefaultSchemeName;
    public GraphThemeSettings Theme { get; set; } = new();
    public GraphLayoutSettings Layout { get; set; } = new();
    public QueryBuilderSettings QueryBuilder { get; set; } = new();
}
