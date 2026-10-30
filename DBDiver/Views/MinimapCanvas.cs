using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DBDiver.ViewModels;

namespace DBDiver.Views;

public class MinimapCanvas : Control
{
    private const double Inset = 6;

    public MainWindow? OwnerWindow { get; set; }

    public override void Render(DrawingContext context)
    {
        if (OwnerWindow?.DataContext is not MainViewModel vm || vm.NodeIndex.Count == 0)
            return;

        var graphBounds = OwnerWindow.ComputeGraphWorldBounds();
        if (graphBounds.Width <= 0 || graphBounds.Height <= 0)
            return;

        var width = Bounds.Width - Inset * 2;
        var height = Bounds.Height - Inset * 2;
        var scale = Math.Min(width / graphBounds.Width, height / graphBounds.Height);
        var offsetX = Inset + (width - graphBounds.Width * scale) / 2;
        var offsetY = Inset + (height - graphBounds.Height * scale) / 2;

        var nodeBrush = GetResourceBrush("GraphEdgeDimmedStrokeBrush");
        var hubBrush = GetResourceBrush("GraphNodeHubBorderBrush");
        var selectedBrush = GetResourceBrush("GraphNodeSelectedBorderBrush");

        foreach (var node in vm.GraphNodes)
        {
            var x = offsetX + (node.X - graphBounds.X) * scale;
            var y = offsetY + (node.Y - graphBounds.Y) * scale;
            var widthScaled = Math.Max(node.Width * scale, 2);
            var heightScaled = Math.Max(node.Height * scale, 2);
            var brush = node.IsSelected ? selectedBrush : node.IsHub ? hubBrush : nodeBrush;
            context.FillRectangle(brush, new Rect(x, y, widthScaled, heightScaled), 1);
        }

        var viewportRect = OwnerWindow.GetMinimapViewportRect(graphBounds, scale, offsetX, offsetY);
        var viewportBrush = new SolidColorBrush(GetResourceColor("MinimapViewportBrush"));
        var viewportPen = new Pen(GetResourceBrush("GraphEdgeHighlightStrokeBrush"), 1);
        context.FillRectangle(viewportBrush, viewportRect);
        context.DrawRectangle(viewportPen, viewportRect, 2);
    }

    private Avalonia.Media.IBrush GetResourceBrush(string resourceKey)
    {
        if (this.TryGetResource(resourceKey, ActualThemeVariant, out var resource) == true
            && resource is Avalonia.Media.IBrush brush)
            return brush;

        return Avalonia.Media.Brushes.Transparent;
    }

    private Color GetResourceColor(string resourceKey)
    {
        if (this.TryGetResource(resourceKey, ActualThemeVariant, out var resource) == true
            && resource is Avalonia.Media.SolidColorBrush brush)
            return brush.Color;

        return Colors.Transparent;
    }
}
