using Avalonia.Media;

namespace DBDiver.Models;

public class GraphThemeSettings
{
    public string AppBackground { get; set; } = GetResourceColor("AppBackgroundBrush");
    public string ToolbarBackground { get; set; } = GetResourceColor("ToolbarBackgroundBrush");
    public string ToolbarText { get; set; } = GetResourceColor("ToolbarTextBrush");
    public string ToolbarButtonBackground { get; set; } = GetResourceColor("ToolbarButtonBackgroundBrush");
    public string ToolbarButtonForeground { get; set; } = GetResourceColor("ToolbarButtonForegroundBrush");
    public string ToolbarButtonBorder { get; set; } = GetResourceColor("ToolbarButtonBorderBrush");
    public string ToolbarButtonHoverBackground { get; set; } = GetResourceColor("ToolbarButtonHoverBackgroundBrush");
    public string ToolbarButtonHoverBorder { get; set; } = GetResourceColor("ToolbarButtonHoverBorderBrush");
    public string GraphBackground { get; set; } = GetResourceColor("GraphBackgroundBrush");
    public string GraphNodeBackground { get; set; } = GetResourceColor("GraphNodeBackgroundBrush");
    public string GraphNodeBorder { get; set; } = GetResourceColor("GraphNodeBorderBrush");
    public string GraphNodeText { get; set; } = GetResourceColor("GraphNodeTextBrush");
    public string GraphNodeHoverBackground { get; set; } = GetResourceColor("GraphNodeHoverBackgroundBrush");
    public string GraphNodeHoverBorder { get; set; } = GetResourceColor("GraphNodeHoverBorderBrush");
    public string GraphNodeHighlightBackground { get; set; } = GetResourceColor("GraphNodeHighlightBackgroundBrush");
    public string GraphNodeHighlightBorder { get; set; } = GetResourceColor("GraphNodeHighlightBorderBrush");
    public string GraphNodeSelectedBackground { get; set; } = GetResourceColor("GraphNodeSelectedBackgroundBrush");
    public string GraphNodeSelectedBorder { get; set; } = GetResourceColor("GraphNodeSelectedBorderBrush");
    public string GraphNodeHubBackground { get; set; } = GetResourceColor("GraphNodeHubBackgroundBrush");
    public string GraphNodeHubBorder { get; set; } = GetResourceColor("GraphNodeHubBorderBrush");
    public string GraphNodeCustomSourceBorder { get; set; } = GetResourceColor("GraphNodeCustomSourceBorderBrush");
    public string GraphEdgeStroke { get; set; } = GetResourceColor("GraphEdgeStrokeBrush");
    public string GraphEdgeHighlightStroke { get; set; } = GetResourceColor("GraphEdgeHighlightStrokeBrush");
    public string GraphEdgeDimmedStroke { get; set; } = GetResourceColor("GraphEdgeDimmedStrokeBrush");
    public string GraphEdgeCustomStroke { get; set; } = GetResourceColor("GraphEdgeCustomStrokeBrush");
    public string StatusbarBackground { get; set; } = GetResourceColor("StatusbarBackgroundBrush");
    public string StatusbarText { get; set; } = GetResourceColor("StatusbarTextBrush");
    public string SidebarBackground { get; set; } = GetResourceColor("SidebarBackgroundBrush");
    public string SidebarBorder { get; set; } = GetResourceColor("SidebarBorderBrush");
    public string SidebarText { get; set; } = GetResourceColor("SidebarTextBrush");
    public string SidebarHeadingText { get; set; } = GetResourceColor("SidebarHeadingTextBrush");
    public string SidebarItemBackground { get; set; } = GetResourceColor("SidebarItemBackgroundBrush");
    public string SidebarItemBorder { get; set; } = GetResourceColor("SidebarItemBorderBrush");
    public string SidebarItemText { get; set; } = GetResourceColor("SidebarItemTextBrush");
    public string MinimapBackground { get; set; } = GetResourceColor("MinimapBackgroundBrush");
    public string MinimapBorder { get; set; } = GetResourceColor("MinimapBorderBrush");
    public string MinimapViewport { get; set; } = GetResourceColor("GraphEdgeHighlightStrokeBrush");

    public string SettingsText { get; set; } = GetResourceColor("ToolbarTextBrush");
    public string SettingsInputText { get; set; } = GetResourceColor("ToolbarButtonForegroundBrush");
    public string SettingsButtonText { get; set; } = GetResourceColor("ToolbarButtonForegroundBrush");

    private static string GetResourceColor(string resourceKey)
    {
        if (Avalonia.Application.Current?.TryGetResource(
                resourceKey,
                Avalonia.Application.Current.ActualThemeVariant,
                out var resource) == true
            && resource is SolidColorBrush brush)
            return brush.Color.ToString();

        return string.Empty;
    }
}
