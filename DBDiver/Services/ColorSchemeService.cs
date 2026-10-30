namespace DBDiver.Services;

public static class ColorSchemeService
{
    public const string DefaultSchemeName = "Default";

    public static IReadOnlyList<Models.ColorScheme> Schemes { get; } =
    [
        CreateDefault(),
        CreateNordDusk(),
        CreateSolarizedLight()
    ];

    public static Models.ColorScheme Get(string? name)
    {
        return Schemes.FirstOrDefault(scheme => scheme.Name == name) ?? Schemes[0];
    }

    public static Models.GraphThemeSettings CreateTheme(string? name) => CreateTheme(Get(name));

    public static void ApplyThemeVariant(string? name)
    {
        if (Avalonia.Application.Current is { } application)
            application.RequestedThemeVariant = Get(name).Variant;
    }

    public static Models.GraphThemeSettings CreateTheme(Models.ColorScheme scheme)
    {
        return new Models.GraphThemeSettings
        {
            AppBackground = scheme.Colors[nameof(Models.GraphThemeSettings.AppBackground)],
            ToolbarBackground = scheme.Colors[nameof(Models.GraphThemeSettings.ToolbarBackground)],
            ToolbarText = scheme.Colors[nameof(Models.GraphThemeSettings.ToolbarText)],
            ToolbarButtonBackground = scheme.Colors[nameof(Models.GraphThemeSettings.ToolbarButtonBackground)],
            ToolbarButtonForeground = scheme.Colors[nameof(Models.GraphThemeSettings.ToolbarButtonForeground)],
            ToolbarButtonBorder = scheme.Colors[nameof(Models.GraphThemeSettings.ToolbarButtonBorder)],
            ToolbarButtonHoverBackground = scheme.Colors[nameof(Models.GraphThemeSettings.ToolbarButtonHoverBackground)],
            ToolbarButtonHoverBorder = scheme.Colors[nameof(Models.GraphThemeSettings.ToolbarButtonHoverBorder)],
            GraphBackground = scheme.Colors[nameof(Models.GraphThemeSettings.GraphBackground)],
            GraphNodeBackground = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeBackground)],
            GraphNodeBorder = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeBorder)],
            GraphNodeText = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeText)],
            GraphNodeHoverBackground = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeHoverBackground)],
            GraphNodeHoverBorder = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeHoverBorder)],
            GraphNodeHighlightBackground = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeHighlightBackground)],
            GraphNodeHighlightBorder = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeHighlightBorder)],
            GraphNodeSelectedBackground = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeSelectedBackground)],
            GraphNodeSelectedBorder = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeSelectedBorder)],
            GraphNodeHubBackground = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeHubBackground)],
            GraphNodeHubBorder = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeHubBorder)],
            GraphNodeCustomSourceBorder = scheme.Colors[nameof(Models.GraphThemeSettings.GraphNodeCustomSourceBorder)],
            GraphEdgeStroke = scheme.Colors[nameof(Models.GraphThemeSettings.GraphEdgeStroke)],
            GraphEdgeHighlightStroke = scheme.Colors[nameof(Models.GraphThemeSettings.GraphEdgeHighlightStroke)],
            GraphEdgeDimmedStroke = scheme.Colors[nameof(Models.GraphThemeSettings.GraphEdgeDimmedStroke)],
            GraphEdgeCustomStroke = scheme.Colors[nameof(Models.GraphThemeSettings.GraphEdgeCustomStroke)],
            StatusbarBackground = scheme.Colors[nameof(Models.GraphThemeSettings.StatusbarBackground)],
            StatusbarText = scheme.Colors[nameof(Models.GraphThemeSettings.StatusbarText)],
            SidebarBackground = scheme.Colors[nameof(Models.GraphThemeSettings.SidebarBackground)],
            SidebarBorder = scheme.Colors[nameof(Models.GraphThemeSettings.SidebarBorder)],
            SidebarText = scheme.Colors[nameof(Models.GraphThemeSettings.SidebarText)],
            SidebarHeadingText = scheme.Colors[nameof(Models.GraphThemeSettings.SidebarHeadingText)],
            SidebarItemBackground = scheme.Colors[nameof(Models.GraphThemeSettings.SidebarItemBackground)],
            SidebarItemBorder = scheme.Colors[nameof(Models.GraphThemeSettings.SidebarItemBorder)],
            SidebarItemText = scheme.Colors[nameof(Models.GraphThemeSettings.SidebarItemText)],
            MinimapBackground = scheme.Colors[nameof(Models.GraphThemeSettings.MinimapBackground)],
            MinimapBorder = scheme.Colors[nameof(Models.GraphThemeSettings.MinimapBorder)],
            MinimapViewport = scheme.Colors[nameof(Models.GraphThemeSettings.MinimapViewport)],
            SettingsText = scheme.Colors[nameof(Models.GraphThemeSettings.SettingsText)],
            SettingsInputText = scheme.Colors[nameof(Models.GraphThemeSettings.SettingsInputText)],
            SettingsButtonText = scheme.Colors[nameof(Models.GraphThemeSettings.SettingsButtonText)]
        };
    }

    private static Models.ColorScheme CreateDefault() => new(DefaultSchemeName, Avalonia.Styling.ThemeVariant.Dark, new Dictionary<string, string>
    {
        [nameof(Models.GraphThemeSettings.AppBackground)] = "#0F172A",
        [nameof(Models.GraphThemeSettings.ToolbarBackground)] = "#0F172A",
        [nameof(Models.GraphThemeSettings.ToolbarText)] = "#E2E8F0",
        [nameof(Models.GraphThemeSettings.ToolbarButtonBackground)] = "#1E293B",
        [nameof(Models.GraphThemeSettings.ToolbarButtonForeground)] = "#E2E8F0",
        [nameof(Models.GraphThemeSettings.ToolbarButtonBorder)] = "#334155",
        [nameof(Models.GraphThemeSettings.ToolbarButtonHoverBackground)] = "#243449",
        [nameof(Models.GraphThemeSettings.ToolbarButtonHoverBorder)] = "#60A5FA",
        [nameof(Models.GraphThemeSettings.GraphBackground)] = "#0B1220",
        [nameof(Models.GraphThemeSettings.GraphNodeBackground)] = "#1E293B",
        [nameof(Models.GraphThemeSettings.GraphNodeBorder)] = "#334155",
        [nameof(Models.GraphThemeSettings.GraphNodeText)] = "#E2E8F0",
        [nameof(Models.GraphThemeSettings.GraphNodeHoverBackground)] = "#243449",
        [nameof(Models.GraphThemeSettings.GraphNodeHoverBorder)] = "#60A5FA",
        [nameof(Models.GraphThemeSettings.GraphNodeHighlightBackground)] = "#1E3A5F",
        [nameof(Models.GraphThemeSettings.GraphNodeHighlightBorder)] = "#60A5FA",
        [nameof(Models.GraphThemeSettings.GraphNodeSelectedBackground)] = "#1F2937",
        [nameof(Models.GraphThemeSettings.GraphNodeSelectedBorder)] = "#38BDF8",
        [nameof(Models.GraphThemeSettings.GraphNodeHubBackground)] = "#17253D",
        [nameof(Models.GraphThemeSettings.GraphNodeHubBorder)] = "#7DD3FC",
        [nameof(Models.GraphThemeSettings.GraphNodeCustomSourceBorder)] = "#F59E0B",
        [nameof(Models.GraphThemeSettings.GraphEdgeStroke)] = "#475569",
        [nameof(Models.GraphThemeSettings.GraphEdgeHighlightStroke)] = "#60A5FA",
        [nameof(Models.GraphThemeSettings.GraphEdgeDimmedStroke)] = "#334155",
        [nameof(Models.GraphThemeSettings.GraphEdgeCustomStroke)] = "#F59E0B",
        [nameof(Models.GraphThemeSettings.StatusbarBackground)] = "#111827",
        [nameof(Models.GraphThemeSettings.StatusbarText)] = "#9CA3AF",
        [nameof(Models.GraphThemeSettings.SidebarBackground)] = "#111827",
        [nameof(Models.GraphThemeSettings.SidebarBorder)] = "#1F2937",
        [nameof(Models.GraphThemeSettings.SidebarText)] = "#CBD5E1",
        [nameof(Models.GraphThemeSettings.SidebarHeadingText)] = "#E2E8F0",
        [nameof(Models.GraphThemeSettings.SidebarItemBackground)] = "#1E293B",
        [nameof(Models.GraphThemeSettings.SidebarItemBorder)] = "#334155",
        [nameof(Models.GraphThemeSettings.SidebarItemText)] = "#CBD5E1",
        [nameof(Models.GraphThemeSettings.MinimapBackground)] = "#CC0B1220",
        [nameof(Models.GraphThemeSettings.MinimapBorder)] = "#334155",
        [nameof(Models.GraphThemeSettings.MinimapViewport)] = "#334155",
        [nameof(Models.GraphThemeSettings.SettingsText)] = "#E2E8F0",
        [nameof(Models.GraphThemeSettings.SettingsInputText)] = "#E2E8F0",
        [nameof(Models.GraphThemeSettings.SettingsButtonText)] = "#E2E8F0"
    });

    private static Models.ColorScheme CreateNordDusk() => new("Nord Dusk", Avalonia.Styling.ThemeVariant.Dark, new Dictionary<string, string>
    {
        [nameof(Models.GraphThemeSettings.AppBackground)] = "#2E3440",
        [nameof(Models.GraphThemeSettings.ToolbarBackground)] = "#2E3440",
        [nameof(Models.GraphThemeSettings.ToolbarText)] = "#ECEFF4",
        [nameof(Models.GraphThemeSettings.ToolbarButtonBackground)] = "#3B4252",
        [nameof(Models.GraphThemeSettings.ToolbarButtonForeground)] = "#E5E9F0",
        [nameof(Models.GraphThemeSettings.ToolbarButtonBorder)] = "#4C566A",
        [nameof(Models.GraphThemeSettings.ToolbarButtonHoverBackground)] = "#434C5E",
        [nameof(Models.GraphThemeSettings.ToolbarButtonHoverBorder)] = "#88C0D0",
        [nameof(Models.GraphThemeSettings.GraphBackground)] = "#272C36",
        [nameof(Models.GraphThemeSettings.GraphNodeBackground)] = "#3B4252",
        [nameof(Models.GraphThemeSettings.GraphNodeBorder)] = "#4C566A",
        [nameof(Models.GraphThemeSettings.GraphNodeText)] = "#ECEFF4",
        [nameof(Models.GraphThemeSettings.GraphNodeHoverBackground)] = "#434C5E",
        [nameof(Models.GraphThemeSettings.GraphNodeHoverBorder)] = "#88C0D0",
        [nameof(Models.GraphThemeSettings.GraphNodeHighlightBackground)] = "#434C5E",
        [nameof(Models.GraphThemeSettings.GraphNodeHighlightBorder)] = "#88C0D0",
        [nameof(Models.GraphThemeSettings.GraphNodeSelectedBackground)] = "#434C5E",
        [nameof(Models.GraphThemeSettings.GraphNodeSelectedBorder)] = "#A3BE8C",
        [nameof(Models.GraphThemeSettings.GraphNodeHubBackground)] = "#434C5E",
        [nameof(Models.GraphThemeSettings.GraphNodeHubBorder)] = "#81A1C1",
        [nameof(Models.GraphThemeSettings.GraphNodeCustomSourceBorder)] = "#EBCB8B",
        [nameof(Models.GraphThemeSettings.GraphEdgeStroke)] = "#4C566A",
        [nameof(Models.GraphThemeSettings.GraphEdgeHighlightStroke)] = "#88C0D0",
        [nameof(Models.GraphThemeSettings.GraphEdgeDimmedStroke)] = "#3B4252",
        [nameof(Models.GraphThemeSettings.GraphEdgeCustomStroke)] = "#EBCB8B",
        [nameof(Models.GraphThemeSettings.StatusbarBackground)] = "#3B4252",
        [nameof(Models.GraphThemeSettings.StatusbarText)] = "#D8DEE9",
        [nameof(Models.GraphThemeSettings.SidebarBackground)] = "#3B4252",
        [nameof(Models.GraphThemeSettings.SidebarBorder)] = "#4C566A",
        [nameof(Models.GraphThemeSettings.SidebarText)] = "#D8DEE9",
        [nameof(Models.GraphThemeSettings.SidebarHeadingText)] = "#ECEFF4",
        [nameof(Models.GraphThemeSettings.SidebarItemBackground)] = "#3B4252",
        [nameof(Models.GraphThemeSettings.SidebarItemBorder)] = "#4C566A",
        [nameof(Models.GraphThemeSettings.SidebarItemText)] = "#D8DEE9",
        [nameof(Models.GraphThemeSettings.MinimapBackground)] = "#CC272C36",
        [nameof(Models.GraphThemeSettings.MinimapBorder)] = "#4C566A",
        [nameof(Models.GraphThemeSettings.MinimapViewport)] = "#88C0D0",
        [nameof(Models.GraphThemeSettings.SettingsText)] = "#ECEFF4",
        [nameof(Models.GraphThemeSettings.SettingsInputText)] = "#E5E9F0",
        [nameof(Models.GraphThemeSettings.SettingsButtonText)] = "#E5E9F0"
    });

    private static Models.ColorScheme CreateSolarizedLight() => new("Solarized Light", Avalonia.Styling.ThemeVariant.Light, new Dictionary<string, string>
    {
        [nameof(Models.GraphThemeSettings.AppBackground)] = "#FDF6E3",
        [nameof(Models.GraphThemeSettings.ToolbarBackground)] = "#FDF6E3",
        [nameof(Models.GraphThemeSettings.ToolbarText)] = "#073642",
        [nameof(Models.GraphThemeSettings.ToolbarButtonBackground)] = "#EEE8D5",
        [nameof(Models.GraphThemeSettings.ToolbarButtonForeground)] = "#073642",
        [nameof(Models.GraphThemeSettings.ToolbarButtonBorder)] = "#93A1A1",
        [nameof(Models.GraphThemeSettings.ToolbarButtonHoverBackground)] = "#F5EFD9",
        [nameof(Models.GraphThemeSettings.ToolbarButtonHoverBorder)] = "#268BD2",
        [nameof(Models.GraphThemeSettings.GraphBackground)] = "#EEE8D5",
        [nameof(Models.GraphThemeSettings.GraphNodeBackground)] = "#EEE8D5",
        [nameof(Models.GraphThemeSettings.GraphNodeBorder)] = "#93A1A1",
        [nameof(Models.GraphThemeSettings.GraphNodeText)] = "#073642",
        [nameof(Models.GraphThemeSettings.GraphNodeHoverBackground)] = "#F5EFD9",
        [nameof(Models.GraphThemeSettings.GraphNodeHoverBorder)] = "#268BD2",
        [nameof(Models.GraphThemeSettings.GraphNodeHighlightBackground)] = "#EEE8D5",
        [nameof(Models.GraphThemeSettings.GraphNodeHighlightBorder)] = "#268BD2",
        [nameof(Models.GraphThemeSettings.GraphNodeSelectedBackground)] = "#FDF6E3",
        [nameof(Models.GraphThemeSettings.GraphNodeSelectedBorder)] = "#268BD2",
        [nameof(Models.GraphThemeSettings.GraphNodeHubBackground)] = "#EEE8D5",
        [nameof(Models.GraphThemeSettings.GraphNodeHubBorder)] = "#2AA198",
        [nameof(Models.GraphThemeSettings.GraphNodeCustomSourceBorder)] = "#B58900",
        [nameof(Models.GraphThemeSettings.GraphEdgeStroke)] = "#93A1A1",
        [nameof(Models.GraphThemeSettings.GraphEdgeHighlightStroke)] = "#268BD2",
        [nameof(Models.GraphThemeSettings.GraphEdgeDimmedStroke)] = "#D3D0C8",
        [nameof(Models.GraphThemeSettings.GraphEdgeCustomStroke)] = "#B58900",
        [nameof(Models.GraphThemeSettings.StatusbarBackground)] = "#EFEEDD",
        [nameof(Models.GraphThemeSettings.StatusbarText)] = "#586E75",
        [nameof(Models.GraphThemeSettings.SidebarBackground)] = "#EEE8D5",
        [nameof(Models.GraphThemeSettings.SidebarBorder)] = "#93A1A1",
        [nameof(Models.GraphThemeSettings.SidebarText)] = "#586E75",
        [nameof(Models.GraphThemeSettings.SidebarHeadingText)] = "#073642",
        [nameof(Models.GraphThemeSettings.SidebarItemBackground)] = "#EEE8D5",
        [nameof(Models.GraphThemeSettings.SidebarItemBorder)] = "#93A1A1",
        [nameof(Models.GraphThemeSettings.SidebarItemText)] = "#073642",
        [nameof(Models.GraphThemeSettings.MinimapBackground)] = "#CCFDF6E3",
        [nameof(Models.GraphThemeSettings.MinimapBorder)] = "#93A1A1",
        [nameof(Models.GraphThemeSettings.MinimapViewport)] = "#268BD2",
        [nameof(Models.GraphThemeSettings.SettingsText)] = "#073642",
        [nameof(Models.GraphThemeSettings.SettingsInputText)] = "#073642",
        [nameof(Models.GraphThemeSettings.SettingsButtonText)] = "#073642"
    });
}
