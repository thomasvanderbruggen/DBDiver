using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DBDiver.Models;
using DBDiver.ViewModels;

namespace DBDiver.Views;

public partial class SettingsOverlay : UserControl
{
    private readonly SettingsViewModel _viewModel;
    private ColorPicker? _sharedPicker;
    private Flyout? _pickerFlyout;
    private string? _editingPropertyName;
    private string? _copiedColor;

    public event Action? CloseRequested;

    public SettingsOverlay(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        BuildThemeEditor();
        LayoutTabControl.SelectionChanged += OnLayoutTabSelected;
        SaveButton.Click += OnSaveClick;
        ResetButton.Click += OnResetClick;
        CloseButton.Click += (_, _) => CloseRequested?.Invoke();
    }

    private void OnLayoutTabSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (LayoutTabControl.SelectedItem is TabItem { Header: "Layout Physics" } && LayoutPanel.Children.Count == 0)
            BuildLayoutEditor();
    }

    private void BuildThemeEditor()
    {
        var properties = typeof(GraphThemeSettings).GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.Name)
            .ToList();

        ThemeColorList.ItemsSource = properties;
        ThemeColorList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((name, _) =>
        {
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Tag = name };
            var label = new TextBlock { Text = name, Width = 240, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var current = (string?)typeof(GraphThemeSettings).GetProperty(name)?.GetValue(_viewModel.Theme);
            var input = new TextBox { Width = 120, Text = current };
            var preview = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new Avalonia.CornerRadius(4),
                BorderBrush = Avalonia.Media.Brushes.Gray,
                BorderThickness = new Avalonia.Thickness(1),
                Background = CreateBrush(current)
            };
            var pickButton = new Button { Content = "Pick…", Padding = new Avalonia.Thickness(8, 4) };
            pickButton.Click += (_, _) => OpenPickerFlyout(name, pickButton);

            input.TextChanged += (_, _) =>
            {
                if (Avalonia.Media.Color.TryParse(input.Text ?? string.Empty, out Avalonia.Media.Color parsed))
                {
                    typeof(GraphThemeSettings).GetProperty(name)?.SetValue(_viewModel.Theme, input.Text);
                    preview.Background = new Avalonia.Media.SolidColorBrush(parsed);
                    _viewModel.RaiseThemeChanged();
                }
            };

            var copyButton = new Button { Content = "Copy", Padding = new Avalonia.Thickness(8, 4) };
            copyButton.Click += (_, _) => CopyColorSetting(name);
            var pasteButton = new Button { Content = "Paste", Padding = new Avalonia.Thickness(8, 4) };
            pasteButton.Click += (_, _) => PasteColorSetting(name);

            panel.Children.Add(label);
            panel.Children.Add(preview);
            panel.Children.Add(input);
            panel.Children.Add(pickButton);
            panel.Children.Add(copyButton);
            panel.Children.Add(pasteButton);
            return panel;
        });
    }

    private void OpenPickerFlyout(string propertyName, Button sourceButton)
    {
        _editingPropertyName = propertyName;
        _sharedPicker ??= new ColorPicker { Width = 340 };
        _pickerFlyout ??= new Flyout { Content = _sharedPicker };

        var currentValue = (string?)typeof(GraphThemeSettings).GetProperty(propertyName)?.GetValue(_viewModel.Theme);
        if (Avalonia.Media.Color.TryParse(currentValue ?? string.Empty, out Avalonia.Media.Color color))
            _sharedPicker.Color = color;

        _sharedPicker.ColorChanged -= OnPickerColorChanged;
        _sharedPicker.ColorChanged += OnPickerColorChanged;
        _pickerFlyout.ShowAt(sourceButton);
    }

    private void OnPickerColorChanged(object? sender, ColorChangedEventArgs args)
    {
        if (_editingPropertyName is null)
            return;

        var hex = args.NewColor.ToString();
        typeof(GraphThemeSettings).GetProperty(_editingPropertyName)?.SetValue(_viewModel.Theme, hex);
        _viewModel.RaiseThemeChanged();

        var targetRow = ThemeColorList.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(row => ReferenceEquals(row.Tag, _editingPropertyName));
        if (targetRow is null)
            return;

        if (targetRow.Children.OfType<TextBox>().FirstOrDefault() is { } input)
            input.Text = hex;
        if (targetRow.Children.OfType<Border>().FirstOrDefault() is { } preview)
            preview.Background = new Avalonia.Media.SolidColorBrush(args.NewColor);
    }

    private void BuildLayoutEditor()
    {
        LayoutPanel.Children.Clear();
        foreach (var property in typeof(GraphLayoutSettings).GetProperties())
        {
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            var label = new TextBlock { Text = property.Name, Width = 200, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            ToolTip.SetTip(panel, GetLayoutDescription(property.Name));
            var numeric = new NumericUpDown { Width = 160, Minimum = 0, Maximum = 100000 };
            numeric.Value = Convert.ToDecimal(property.GetValue(_viewModel.Layout));
            numeric.ValueChanged += (_, args) =>
            {
                var value = Convert.ChangeType(args.NewValue ?? 0, property.PropertyType);
                property.SetValue(_viewModel.Layout, value);
                _viewModel.RaiseLayoutChanged();
            };

            panel.Children.Add(label);
            panel.Children.Add(numeric);
            LayoutPanel.Children.Add(panel);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.Save();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (LayoutTabControl.SelectedItem is TabItem { Header: "Appearance" })
            _viewModel.ResetTheme();
        else
            _viewModel.ResetLayout();

        BuildThemeEditor();
        BuildLayoutEditor();
    }

    private void CopyColorSetting(string propertyName)
    {
        _copiedColor = (string?)typeof(GraphThemeSettings).GetProperty(propertyName)?.GetValue(_viewModel.Theme);
    }

    private void PasteColorSetting(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(_copiedColor) || !Avalonia.Media.Color.TryParse(_copiedColor, out Avalonia.Media.Color _))
            return;

        typeof(GraphThemeSettings).GetProperty(propertyName)?.SetValue(_viewModel.Theme, _copiedColor);
        _viewModel.RaiseThemeChanged();
        BuildThemeEditor();
    }

    private static Avalonia.Media.SolidColorBrush? CreateBrush(string? colorValue)
    {
        return Avalonia.Media.Color.TryParse(colorValue ?? string.Empty, out Avalonia.Media.Color color)
            ? new Avalonia.Media.SolidColorBrush(color)
            : null;
    }

    private static string GetLayoutDescription(string propertyName) => propertyName switch
    {
        nameof(GraphLayoutSettings.RepulsionStrength) => "Pushes unconnected nodes apart. Increase for larger or looser schemas.",
        nameof(GraphLayoutSettings.SpringStrength) => "How strongly connected tables pull toward their ideal distance.",
        nameof(GraphLayoutSettings.SpringLength) => "Preferred distance between connected tables.",
        nameof(GraphLayoutSettings.Damping) => "Movement damping per layout pass. Lower values settle faster.",
        nameof(GraphLayoutSettings.MinSeparation) => "Minimum logical distance enforced between node cards.",
        nameof(GraphLayoutSettings.MaxIterations) => "Maximum number of layout solver passes. Higher can improve spacing but takes longer.",
        nameof(GraphLayoutSettings.HubSpacing) => "Extra spacing between cluster hubs in the graph.",
        nameof(GraphLayoutSettings.SemanticBuffer) => "Additional spacing reserved so enlarged labels do not overlap.",
        _ => propertyName
    };
}
