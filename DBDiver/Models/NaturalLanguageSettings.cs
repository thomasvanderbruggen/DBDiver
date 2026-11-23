namespace DBDiver.Models;

public class NaturalLanguageSettings
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ModelName { get; set; } = string.Empty;
}
