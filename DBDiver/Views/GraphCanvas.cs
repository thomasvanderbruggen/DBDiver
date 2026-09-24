using Avalonia;
using Avalonia.Controls;

namespace DBDiver.Views;

public class GraphCanvas : Panel
{
    public static readonly AttachedProperty<double> XProperty =
        AvaloniaProperty.RegisterAttached<GraphCanvas, Control, double>("X");

    public static readonly AttachedProperty<double> YProperty =
        AvaloniaProperty.RegisterAttached<GraphCanvas, Control, double>("Y");

    public static double GetX(Control control) => control.GetValue(XProperty);
    public static void SetX(Control control, double value) => control.SetValue(XProperty, value);
    public static double GetY(Control control) => control.GetValue(YProperty);
    public static void SetY(Control control, double value) => control.SetValue(YProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        double maxX = 0, maxY = 0;
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
            var x = child.GetValue(XProperty);
            var y = child.GetValue(YProperty);
            maxX = Math.Max(maxX, x + child.DesiredSize.Width);
            maxY = Math.Max(maxY, y + child.DesiredSize.Height);
        }

        return new Size(maxX, maxY);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double maxX = 0, maxY = 0;
        foreach (var child in Children)
        {
            var x = child.GetValue(XProperty);
            var y = child.GetValue(YProperty);
            var desired = child.DesiredSize;
            child.Arrange(new Rect(x, y, desired.Width, desired.Height));
            maxX = Math.Max(maxX, x + desired.Width);
            maxY = Math.Max(maxY, y + desired.Height);
        }

        return new Size(maxX, maxY);
    }
}
