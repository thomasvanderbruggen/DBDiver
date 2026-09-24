using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Diagnostics;
using DBDiver.Models;
using DBDiver.ViewModels;

namespace DBDiver.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private ColorPicker? _sharedPicker;
    private Flyout? _pickerFlyout;
    private string? _editingPropertyName;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        var constructorStopwatch = Stopwatch.StartNew();
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        BuildThemeEditor();
        AttachedToVisualTree += (_, _) => Serilog.Log.Information("[SettingsTiming] Settings window AttachedToVisualTree fired");
        DetachedFromVisualTree += (_, _) => Serilog.Log.Information("[SettingsTiming] Settings window DetachedFromVisualTree fired");
        LayoutTabControl.SelectionChanged += OnLayoutTabSelected;
        SaveButton.Click += OnSaveClick;
        CloseButton.Click += (_, _) => Hide();
        ResetButton.Click += OnResetClick;
        constructorStopwatch.Stop();
        Serilog.Log.Information("[SettingsTiming] SettingsWindow constructor completed in {ElapsedMs} ms", constructorStopwatch.ElapsedMilliseconds);
        Loaded += (_, _) => Serilog.Log.Information("[SettingsTiming] SettingsWindow Loaded event fired");
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel = true;
        Hide();
        Serilog.Log.Information("[SettingsTiming] Settings window close intercepted and window hidden");
    }

    private void OnLayoutTabSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (LayoutTabControl.SelectedItem is TabItem { Header: "Layout Physics" } && LayoutPanel.Children.Count == 0)
            BuildLayoutEditor();
        else if (LayoutTabControl.SelectedItem is TabItem { Header: "Query Builder" } && QueryBuilderPanel.Children.Count == 0)
            BuildQueryBuilderEditor();
    }

    private void BuildThemeEditor()
    {
        var buildStopwatch = Stopwatch.StartNew();
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
            var pickButton = new Button
            {
                Content = "Pick…",
                Padding = new Avalonia.Thickness(8, 4)
            };
            pickButton.Click += (_, _) => OpenPickerFlyout(name, pickButton);

            input.TextChanged += (_, _) =>
            {
                if (Avalonia.Media.Color.TryParse(input.Text ?? string.Empty, out Avalonia.Media.Color parsed))
                {
                    var property = typeof(GraphThemeSettings).GetProperty(name);
                    property?.SetValue(_viewModel.Theme, input.Text);
                    if (Avalonia.Media.Color.TryParse(input.Text, out Avalonia.Media.Color previewColor))
                        preview.Background = new Avalonia.Media.SolidColorBrush(previewColor);
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
        buildStopwatch.Stop();
        Serilog.Log.Information("[SettingsTiming] BuildThemeEditor completed in {ElapsedMs} ms", buildStopwatch.ElapsedMilliseconds);
    }

    private void OpenPickerFlyout(string propertyName, Button sourceButton)
    {
        var flyoutStopwatch = Stopwatch.StartNew();
        _editingPropertyName = propertyName;
        if (_sharedPicker is null)
        {
            var pickerStopwatch = Stopwatch.StartNew();
            _sharedPicker = new ColorPicker { Width = 340 };
            pickerStopwatch.Stop();
            Serilog.Log.Information("[SettingsTiming] Shared ColorPicker created in {ElapsedMs} ms", pickerStopwatch.ElapsedMilliseconds);
        }
        _pickerFlyout ??= new Flyout { Content = _sharedPicker };

        var currentValue = (string?)typeof(GraphThemeSettings).GetProperty(propertyName)?.GetValue(_viewModel.Theme);
        if (Avalonia.Media.Color.TryParse(currentValue ?? string.Empty, out Avalonia.Media.Color color))
            _sharedPicker.Color = color;

        _sharedPicker.ColorChanged -= OnPickerColorChanged;
        _sharedPicker.ColorChanged += OnPickerColorChanged;
        _pickerFlyout.ShowAt(sourceButton);
        flyoutStopwatch.Stop();
        Serilog.Log.Information("[SettingsTiming] Flyout ShowAt returned after {ElapsedMs} ms", flyoutStopwatch.ElapsedMilliseconds);
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
        var buildStopwatch = Stopwatch.StartNew();
        LayoutPanel.Children.Clear();
        foreach (var property in typeof(GraphLayoutSettings).GetProperties())
        {
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            var label = new TextBlock { Text = property.Name, Width = 200, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            ToolTip.SetTip(panel, GetLayoutDescription(property.Name));
            var numeric = new NumericUpDown
            {
                Width = 160,
                Minimum = 0,
                Maximum = 100000
            };
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
        buildStopwatch.Stop();
        Serilog.Log.Information("[SettingsTiming] BuildLayoutEditor completed in {ElapsedMs} ms", buildStopwatch.ElapsedMilliseconds);
    }

    private void BuildQueryBuilderEditor()
    {
        var buildStopwatch = Stopwatch.StartNew();
        QueryBuilderPanel.Children.Clear();

        var joinTypePanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        joinTypePanel.Children.Add(new TextBlock { Text = "Default Join Type", Width = 200, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        var joinTypeDropdown = new ComboBox { Width = 160 };
        foreach (QueryBuilderJoinType value in Enum.GetValues(typeof(QueryBuilderJoinType)))
            joinTypeDropdown.Items.Add(value);
        joinTypeDropdown.SelectedItem = _viewModel.QueryBuilder.DefaultJoinType;
        joinTypeDropdown.SelectionChanged += (_, _) =>
        {
            if (joinTypeDropdown.SelectedItem is QueryBuilderJoinType selected)
            {
                _viewModel.QueryBuilder.DefaultJoinType = selected;
                _viewModel.RaiseQueryBuilderChanged();
            }
        };
        joinTypePanel.Children.Add(joinTypeDropdown);
        QueryBuilderPanel.Children.Add(joinTypePanel);

        var colorProperties = new[]
        {
            nameof(QueryBuilderSettings.InnerColor),
            nameof(QueryBuilderSettings.LeftColor),
            nameof(QueryBuilderSettings.RightColor),
            nameof(QueryBuilderSettings.FullColor)
        };
        foreach (var propertyName in colorProperties)
        {
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Tag = propertyName };
            panel.Children.Add(new TextBlock { Text = propertyName.Replace("Color", " Join Color"), Width = 200, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });

            var current = (string?)typeof(QueryBuilderSettings).GetProperty(propertyName)?.GetValue(_viewModel.QueryBuilder);
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
            pickButton.Click += (_, _) => OpenPickerFlyoutForQueryBuilder(propertyName, pickButton);

            input.TextChanged += (_, _) =>
            {
                if (Avalonia.Media.Color.TryParse(input.Text ?? string.Empty, out Avalonia.Media.Color parsed))
                {
                    typeof(QueryBuilderSettings).GetProperty(propertyName)?.SetValue(_viewModel.QueryBuilder, input.Text);
                    if (Avalonia.Media.Color.TryParse(input.Text, out Avalonia.Media.Color previewColor))
                        preview.Background = new Avalonia.Media.SolidColorBrush(previewColor);
                    _viewModel.RaiseQueryBuilderChanged();
                    _viewModel.Save();
                }
            };

            panel.Children.Add(preview);
            panel.Children.Add(input);
            panel.Children.Add(pickButton);
            QueryBuilderPanel.Children.Add(panel);
        }
        buildStopwatch.Stop();
        Serilog.Log.Information("[SettingsTiming] BuildQueryBuilderEditor completed in {ElapsedMs} ms", buildStopwatch.ElapsedMilliseconds);
    }

    private void OpenPickerFlyoutForQueryBuilder(string propertyName, Button sourceButton)
    {
        _editingPropertyName = propertyName;
        _sharedPicker ??= new ColorPicker { Width = 340 };
        _pickerFlyout ??= new Flyout { Content = _sharedPicker };

        var currentValue = (string?)typeof(QueryBuilderSettings).GetProperty(propertyName)?.GetValue(_viewModel.QueryBuilder);
        if (Avalonia.Media.Color.TryParse(currentValue ?? string.Empty, out Avalonia.Media.Color color))
            _sharedPicker.Color = color;

        _sharedPicker.ColorChanged -= OnQueryBuilderPickerColorChanged;
        _sharedPicker.ColorChanged += OnQueryBuilderPickerColorChanged;
        _pickerFlyout.ShowAt(sourceButton);
    }

    private void OnQueryBuilderPickerColorChanged(object? sender, ColorChangedEventArgs args)
    {
        if (_editingPropertyName is null)
            return;

        var hex = args.NewColor.ToString();
        typeof(QueryBuilderSettings).GetProperty(_editingPropertyName)?.SetValue(_viewModel.QueryBuilder, hex);
        _viewModel.RaiseQueryBuilderChanged();
        _viewModel.Save();

        var targetRow = QueryBuilderPanel.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(row => ReferenceEquals(row.Tag, _editingPropertyName));
        if (targetRow is null)
            return;

        if (targetRow.Children.OfType<TextBox>().FirstOrDefault() is { } input)
            input.Text = hex;
        if (targetRow.Children.OfType<Border>().FirstOrDefault() is { } preview)
            preview.Background = new Avalonia.Media.SolidColorBrush(args.NewColor);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.Save();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (LayoutTabControl.SelectedItem is TabItem { Header: "Appearance" })
            _viewModel.ResetTheme();
        else if (LayoutTabControl.SelectedItem is TabItem { Header: "Layout Physics" })
            _viewModel.ResetLayout();
        else if (LayoutTabControl.SelectedItem is TabItem { Header: "Query Builder" })
            _viewModel.ResetQueryBuilder();

        BuildThemeEditor();
        BuildLayoutEditor();
        BuildQueryBuilderEditor();
    }

    private string? _copiedColor;

    private void CopyColorSetting(string propertyName)
    {
        _copiedColor = (string?)typeof(GraphThemeSettings).GetProperty(propertyName)?.GetValue(_viewModel.Theme);
    }

    private void PasteColorSetting(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(_copiedColor))
            return;

        if (!Avalonia.Media.Color.TryParse(_copiedColor, out Avalonia.Media.Color _))
            return;

        var property = typeof(GraphThemeSettings).GetProperty(propertyName);
        property?.SetValue(_viewModel.Theme, _copiedColor);
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
