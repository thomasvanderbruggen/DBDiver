namespace DBDiver.Models;

public class ColorScheme
{
    public string Name { get; }
    public Avalonia.Styling.ThemeVariant Variant { get; }
    public IReadOnlyDictionary<string, string> Colors { get; }

    public ColorScheme(string name, Avalonia.Styling.ThemeVariant variant, IReadOnlyDictionary<string, string> colors)
    {
        Name = name;
        Variant = variant;
        Colors = colors;
    }
}
