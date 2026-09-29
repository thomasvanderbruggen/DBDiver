namespace DBDiver.Models;

public class AppSettings
{
    public bool HasSeenWelcomeGuide { get; set; }
    public GraphThemeSettings Theme { get; set; } = new();
    public GraphLayoutSettings Layout { get; set; } = new();
    public QueryBuilderSettings QueryBuilder { get; set; } = new();
}
