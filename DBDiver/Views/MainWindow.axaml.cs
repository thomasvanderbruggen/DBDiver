using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DBDiver.Services;
using DBDiver.ViewModels;
using DBDiver.Models;

namespace DBDiver.Views;

public partial class MainWindow : Window
{
    private double _zoom = 1.0;
    private const double MinZoom = 0.2;
    private const double MaxZoom = 3.0;
    private const double WheelZoomFactor = 1.1;
    private const double HostPanBoundary = 500;
    private const double NodeBaseFontSize = 13;
    private const double MaxScreenScale = 1.6;
    private const double EdgeHitboxWidth = 32;
    private const double AbbreviatedLabelZoom = 0.45;
    private const int AbbreviatedLabelLength = 14;
    private double _panX;
    private double _panY;
    private bool _isPanning;
    private Point _panStartPosition;
    private double _panStartX;
    private double _panStartY;
    private double _pinchStartZoom;
    private bool _pendingCenterOnGraph;
    private bool _pinchActive;
    private bool _isWaitingForPotentialDragClick;
    private bool _isMinimapDragging;

    public MainWindow()
    {
        InitializeComponent();
        MinimapCanvas.OwnerWindow = this;
        var viewModel = new MainViewModel(new CsvSchemaService());
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.OwnerWindow = this;
        viewModel.GraphLayoutCompleted += CenterOnGraph;
        viewModel.RelationshipChoiceRequested += OnRelationshipChoiceRequested;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(viewModel.GraphNodes))
                MinimapCanvas.InvalidateVisual();
        };
        viewModel.ZoomRequested += OnZoomRequested;
        viewModel.FitGraphRequested += OnFitRequested;
        viewModel.ViewportResetRequested += ResetViewport;
        viewModel.ViewportChanged += () => MinimapCanvas.InvalidateVisual();
        ApplyTransform();
        ClipBorder.LayoutUpdated += OnGraphHostLayoutUpdated;
        ClipBorder.PointerWheelChanged += OnPointerWheel;
        ClipBorder.PointerPressed += OnPointerPressed;
        ClipBorder.PointerMoved += OnPointerMoved;
        ClipBorder.PointerReleased += OnPointerReleased;
        ClipBorder.PointerCaptureLost += OnPointerCaptureLost;
        ClipBorder.AddHandler(PointerPressedEvent, OnGraphPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ClipBorder.AddHandler(PointerReleasedEvent, OnGraphPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ClipBorder.AddHandler(PointerReleasedEvent, OnPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ClipBorder.AddHandler(ScrollGestureEvent, OnScrollGesture, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        GraphHost.AddHandler(InputElement.PinchEvent, OnPinch, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        GraphHost.AddHandler(InputElement.PinchEndedEvent, OnPinchEnded, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        MinimapCanvas.PointerPressed += OnMinimapPointerPressed;
        MinimapCanvas.PointerMoved += OnMinimapPointerMoved;
        MinimapCanvas.PointerReleased += OnMinimapPointerReleased;
        ResetQueryButton.Click += OnResetQueryClick;
        SettingsButton.Click += OnSettingsClick;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.QueryFinished += OnQueryFinished;
        GraphHost.AddHandler(PointerMovedEvent, OnDebugPointerEvent, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        GraphHost.AddHandler(PointerPressedEvent, OnDebugPointerEvent, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void ApplyTransform()
    {
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(_zoom, _zoom));
        group.Children.Add(new TranslateTransform(_panX, _panY));
        GraphHost.RenderTransform = group;
        GraphHost.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        MinimapCanvas.InvalidateVisual();
        UpdateNodeScreenScaling();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(GraphHost);
        if (point.Properties.IsRightButtonPressed && FindNodeFromVisual(e.Source as Control) is not null)
            return;

        if (point.Properties.IsRightButtonPressed)
        {
            _isPanning = true;
            _isWaitingForPotentialDragClick = true;
            _panStartPosition = e.GetPosition(this);
            _panStartX = _panX;
            _panStartY = _panY;
            e.Pointer.Capture(GraphHost);
            e.Handled = true;
        }
    }

    private void OnGraphPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || DataContext is not MainViewModel vm)
            return;

        if (FindNodeFromVisual(e.Source as Control) is { } node)
        {
            if (vm.IsAwaitingCustomJoinTarget)
            {
                _ = vm.CompleteCustomJoinTargetAsync(node);
            }
            else if (vm.IsQueryBuilding)
            {
                vm.AddQueryTable(node);
            }
            else
            {
                vm.ToggleNodeSelection(node);
            }

            e.Handled = true;
        }
        else if (vm.IsQueryBuilding && FindEdgeFromVisual(e.Source as Control) is { } edge)
        {
            vm.CycleJoinType(edge);
            e.Handled = true;
        }
        else if (!vm.IsQueryBuilding && vm.SelectedNode is not null)
        {
            vm.ToggleNodeSelection(vm.SelectedNode);
        }
    }

    private static GraphNode? FindNodeFromVisual(Control? visual)
    {
        Avalonia.StyledElement? current = visual;
        while (current is not null)
        {
            if (current is Control control && control.Tag is GraphNode candidate)
                return candidate;

            current = current.Parent;
        }

        return null;
    }

    private static GraphEdge? FindEdgeFromVisual(Control? visual)
    {
        Avalonia.StyledElement? current = visual;
        while (current is not null)
        {
            if (current is Control control && control.Tag is GraphEdge candidate)
                return candidate;

            current = current.Parent;
        }

        return null;
    }

    private void OnGraphPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning || e.InitialPressMouseButton != MouseButton.Right || !_isWaitingForPotentialDragClick)
            return;

        if (DataContext is MainViewModel vm && vm.SelectedNode is not null)
            vm.ToggleNodeSelection(vm.SelectedNode);

        _isWaitingForPotentialDragClick = false;
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning)
            return;
        var pos = e.GetPosition(this);
        if (_isWaitingForPotentialDragClick)
        {
            var dx = Math.Abs(pos.X - _panStartPosition.X);
            var dy = Math.Abs(pos.Y - _panStartPosition.Y);
            if (dx >= 4 || dy >= 4)
                _isWaitingForPotentialDragClick = false;
        }

        _panX = _panStartX + (pos.X - _panStartPosition.X);
        _panY = _panStartY + (pos.Y - _panStartPosition.Y);
        ClampPan();
        ApplyTransform();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning && e.InitialPressMouseButton == MouseButton.Right)
        {
            _isPanning = false;
            if (_isWaitingForPotentialDragClick)
                _isWaitingForPotentialDragClick = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPanning = false;
        _isWaitingForPotentialDragClick = false;
    }

    private void OnNodeContextMenuRequested(object? sender, RoutedEventArgs e)
    {
        if (sender is Border { Tag: GraphNode node, ContextMenu: { } contextMenu } && DataContext is MainViewModel vm)
        {
            var menuItems = contextMenu.Items.OfType<MenuItem>().ToDictionary(item => item.Header?.ToString() ?? string.Empty, item => item);

            if (menuItems.TryGetValue("Add Custom Join Path", out var addCustomJoinItem))
                addCustomJoinItem.IsEnabled = !vm.IsAwaitingCustomJoinTarget && !vm.IsQueryBuilding;
            if (menuItems.TryGetValue("Build Query", out var buildQueryItem))
                buildQueryItem.IsEnabled = !vm.IsAwaitingCustomJoinTarget && !vm.IsQueryBuilding;
            if (menuItems.TryGetValue("Finish Query", out var finishQueryItem))
                finishQueryItem.IsEnabled = vm.IsQueryBuilding && vm.IsActiveQueryTable(node.Table.Name);
        }

        e.Handled = true;
    }

    private void OnAddCustomJoinClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is Control { Tag: GraphNode node })
            vm.BeginCustomJoin(node);
    }

    private void OnBuildQueryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is Control { Tag: GraphNode node })
            vm.BeginQuery(node);
    }

    private void OnFinishQueryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.FinishQuery();
    }

    private void FindTableFromStartingTableClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is Control {Tag: GraphNode node})
            vm.FindTableFromStartingTable(node);
    }

    private void OnResetQueryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ResetQuery();
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var openStopwatch = System.Diagnostics.Stopwatch.StartNew();
        Serilog.Log.Information("[SettingsTiming] OnSettingsClick start");
        if (DataContext is not MainViewModel vm)
        {
            Serilog.Log.Information("[SettingsTiming] DataContext missing, aborting after {ElapsedMs} ms", openStopwatch.ElapsedMilliseconds);
            return;
        }

        var settingsService = new SettingsService();
        var settingsViewModel = new SettingsViewModel(settingsService, settingsService.Load());
        settingsViewModel.SettingsChanged += settings => vm.ApplySettings(settings);
        var settingsOverlay = new SettingsOverlay(settingsViewModel);
        settingsOverlay.CloseRequested += () => SettingsOverlayHost.IsVisible = false;
        SettingsOverlayPresenter.Content = settingsOverlay;
        SettingsOverlayHost.IsVisible = true;
        
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && e.PropertyName is nameof(MainViewModel.IsQueryBuilding))
            UpdateQueryModeState(vm);
        if (DataContext is MainViewModel vl && e.PropertyName is nameof(MainViewModel.IsJoinPathSearchOpen))
            Dispatcher.UIThread.Post(() => JoinPathSearchTextBox.Focus());
    }

    private void OnQueryFinished()
    {
        if (DataContext is MainViewModel vm)
            ShowQueryResultDialog(vm.GeneratedSql);
    }

    private void OnRelationshipChoiceRequested(IReadOnlyList<IReadOnlyList<DbRelationship>> paths, Action<IReadOnlyList<DbRelationship>> onSelect)
    {
        var flyout = new Flyout
        {
            Placement = PlacementMode.Pointer
        };
        var panel = new StackPanel();
        foreach (var path in paths)
        {
            var rowContent = new StackPanel { Spacing = 2 };
            foreach (var relationship in path)
            {
                rowContent.Children.Add(new TextBlock
                {
                    Text = $"{relationship.SourceTable}.{relationship.SourceColumn} = {relationship.TargetTable}.{relationship.TargetColumn}",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 420
                });
            }

            var item = new Button
            {
                Content = rowContent,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Avalonia.Thickness(12, 6)
            };
            var captured = path;
            item.Click += (_, _) =>
            {
                flyout.Hide();
                onSelect(captured);
            };
            item.Resources["ButtonBackgroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E293B"));
            panel.Children.Add(item);
        }

        flyout.Content = new Border
        {
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0F172A")),
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#334155")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(4),
            Child = panel
        };
        flyout.ShowAt(ClipBorder);
    }

    private void UpdateQueryModeState(MainViewModel vm)
    {
        ResetQueryButton.IsEnabled = vm.IsQueryBuilding || vm.GeneratedSql.Length > 0;
    }

    private void ShowQueryResultDialog(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;

        var dialog = new QueryResultDialog(sql);
        if (Owner is Window owner)
            dialog.ShowDialog(owner);
        else
            dialog.Show();
    }

    private void OnNodePointerEnter(object? sender, PointerEventArgs e)
    {
        if (sender is Control { Tag: GraphNode node } && DataContext is MainViewModel vm)
        {
            vm.SetNodeHover(node, true);
            UpdateNodeScreenScaling();
        }
    }

    private void OnNodePointerLeave(object? sender, PointerEventArgs e)
    {
        if (sender is Control { Tag: GraphNode node } && DataContext is MainViewModel vm)
        {
            vm.SetNodeHover(node, false);
            UpdateNodeScreenScaling();
        }
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        const double PanSpeed = 0.5;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var factor = e.Delta.Y > 0 ? WheelZoomFactor : 1 / WheelZoomFactor;
            var newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
            ZoomAt(newZoom, e.GetPosition(ClipBorder));
            Serilog.Log.Information("Wheel: zoom -> {Zoom:F2} at {Pos}", _zoom, e.GetPosition(ClipBorder));
        }
        else
        {
            var panX = Math.Abs(e.Delta.X) >= 1 ? e.Delta.X * 100 : e.Delta.X * 15;
            var panY = Math.Abs(e.Delta.Y) >= 1 ? e.Delta.Y * 100 : e.Delta.Y * 15;
            _panX -= panX * PanSpeed;
            _panY -= panY * PanSpeed;
            ClampPan();
            ApplyTransform();
            Serilog.Log.Information("Wheel: pan {DeltaX:F2},{DeltaY:F2} ({ScaledX:F0},{ScaledY:F0}) -> ({PanX:F0},{PanY:F0})", e.Delta.X, e.Delta.Y, panX, panY, _panX, _panY);
        }
        e.Handled = true;
    }

    private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
    {
        const double ScrollSpeed = 0.5;
        _panX -= e.Delta.X * ScrollSpeed;
        _panY -= e.Delta.Y * ScrollSpeed;
        ClampPan();
        ApplyTransform();
        Serilog.Log.Information("ScrollGesture: {DeltaX:F2},{DeltaY:F2} -> pan ({PanX:F0},{PanY:F0})", e.Delta.X, e.Delta.Y, _panX, _panY);
        e.Handled = true;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (!_pinchActive)
        {
            _pinchActive = true;
            _pinchStartZoom = _zoom;
        }

        var newZoom = Math.Clamp(_pinchStartZoom * e.Scale, MinZoom, MaxZoom);
        ZoomAt(newZoom, e.ScaleOrigin);
        Serilog.Log.Information("Pinch: scale {Scale:F2} origin {Origin} -> zoom {Zoom:F2}", e.Scale, e.ScaleOrigin, _zoom);
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, RoutedEventArgs e)
    {
        _pinchActive = false;
    }

    private void OnDebugPointerEvent(object? sender, PointerEventArgs e)
    {
        if (e.RoutedEvent == InputElement.PointerMovedEvent && _lastDebugLog.Ticks != 0 && DateTime.Now - _lastDebugLog < TimeSpan.FromMilliseconds(250))
            return;
        _lastDebugLog = DateTime.Now;
        Serilog.Log.Information("Pointer: type={Type}, device={Device}, pressed={Pressed}, pos={Pos}",
            e.Pointer.Type, e.Pointer, e.Pointer.IsPrimary, e.GetPosition(ClipBorder));
    }

    private DateTime _lastDebugLog;

    private void ZoomAt(double newZoom, Point screenPoint, bool relativeToHost = false)
    {
        var oldZoom = _zoom;
        if (relativeToHost)
        {
            var hostPoint = new Point(screenPoint.X, screenPoint.Y);
            _panX += hostPoint.X * (oldZoom - newZoom);
            _panY += hostPoint.Y * (oldZoom - newZoom);
        }
        else
        {
            _panX = screenPoint.X - (screenPoint.X - _panX) * newZoom / oldZoom;
            _panY = screenPoint.Y - (screenPoint.Y - _panY) * newZoom / oldZoom;
        }

        _zoom = newZoom;
        ClampPan();
        ApplyTransform();
    }

    private void CenterOnGraph()
    {
        _pendingCenterOnGraph = true;
    }

    private void OnFitRequested(bool requested)
    {
        if (requested)
            CenterOnGraph();
    }

    private void ResetViewport()
    {
        CenterOnGraph();
        MinimapCanvas.InvalidateVisual();
    }

    private void OnZoomRequested(bool zoomIn)
    {
        if (DataContext is not MainViewModel vm || vm.NodeIndex.Count == 0)
            return;

        var factor = zoomIn ? WheelZoomFactor : 1 / WheelZoomFactor;
        var center = new Point(ClipBorder.Bounds.Width / 2, ClipBorder.Bounds.Height / 2);
        ZoomAt(Math.Clamp(_zoom * factor, MinZoom, MaxZoom), center);
    }

    private void OnGraphHostLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pendingCenterOnGraph)
        {
            PerformCenterOnGraph();
        }
    }

    private void PerformCenterOnGraph()
    {
        var viewportWidth = ClipBorder.Bounds.Width;
        var viewportHeight = ClipBorder.Bounds.Height;
        if (viewportWidth <= 0 || viewportHeight <= 0 || DataContext is not MainViewModel vm || vm.NodeIndex.Count == 0)
            return;

        var minX = vm.NodeIndex.Values.Min(n => n.X);
        var minY = vm.NodeIndex.Values.Min(n => n.Y);
        var maxX = vm.NodeIndex.Values.Max(n => n.X + n.Width);
        var maxY = vm.NodeIndex.Values.Max(n => n.Y + n.Height);
        var graphWidth = Math.Max(maxX - minX, 1);
        var graphHeight = Math.Max(maxY - minY, 1);
        const double CanvasMargin = 100;

        _zoom = Math.Clamp(Math.Min(viewportWidth / graphWidth, viewportHeight / graphHeight), MinZoom, 1.0);
        var scaledWidth = graphWidth * _zoom;
        var scaledHeight = graphHeight * _zoom;
        GraphHost.Width = graphWidth + minX + CanvasMargin;
        GraphHost.Height = graphHeight + minY + CanvasMargin;

        _pendingCenterOnGraph = false;
        _panX = (viewportWidth - scaledWidth) / 2 - minX * _zoom;
        _panY = (viewportHeight - scaledHeight) / 2 - minY * _zoom;
        ClampPan();
        ApplyTransform();

        var childCount = 0;
        string firstChildInfo = "none";
        foreach (var logical in GraphHost.Children)
        {
            if (logical is ItemsControl ic)
            {
                var items = ic.ItemsSource;
                childCount += ic.ItemCount;
                if (firstChildInfo == "none")
                {
                    var panel = ic.ItemsPanelRoot;
                    firstChildInfo = panel is null
                        ? "panel null"
                        : $"panel {panel.Bounds.Width:F0}x{panel.Bounds.Height:F0} children={panel.Children.Count}";
                }
            }
        }

        Serilog.Log.Information(
            "Centered: viewport {VW}x{VH}, graph {GW}x{GH}, zoom {Zoom:F2}, pan ({PX:F0},{PY:F0}), GraphHost {HostW}x{HostH}, items {Items}, first container: {First}",
            viewportWidth, viewportHeight, graphWidth, graphHeight, _zoom, _panX, _panY,
            GraphHost.Width, GraphHost.Height, childCount, firstChildInfo);
    }

    private void ClampPan()
    {
        var hostWidth = double.IsNaN(GraphHost.Width) ? GraphHost.Bounds.Width : GraphHost.Width;
        var hostHeight = double.IsNaN(GraphHost.Height) ? GraphHost.Bounds.Height : GraphHost.Height;
        var scaledWidth = hostWidth * _zoom;
        var scaledHeight = hostHeight * _zoom;

        var viewportWidth = ClipBorder.Bounds.Width;
        var viewportHeight = ClipBorder.Bounds.Height;
        var minPanX = viewportWidth - scaledWidth - HostPanBoundary;
        var minPanY = viewportHeight - scaledHeight - HostPanBoundary;
        var maxPanX = HostPanBoundary;
        var maxPanY = HostPanBoundary;

        _panX = minPanX > maxPanX
            ? (viewportWidth - scaledWidth) / 2
            : Math.Clamp(_panX, minPanX, maxPanX);
        _panY = minPanY > maxPanY
            ? (viewportHeight - scaledHeight) / 2
            : Math.Clamp(_panY, minPanY, maxPanY);
    }

    internal Rect ComputeGraphWorldBounds()
    {
        if (DataContext is not MainViewModel vm || vm.NodeIndex.Count == 0)
            return default;

        var minX = vm.NodeIndex.Values.Min(n => n.X);
        var minY = vm.NodeIndex.Values.Min(n => n.Y);
        var maxX = vm.NodeIndex.Values.Max(n => n.X + n.Width);
        var maxY = vm.NodeIndex.Values.Max(n => n.Y + n.Height);

        if (maxX <= minX || maxY <= minY)
            return default;

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    internal Rect GetMinimapViewportRect(Rect graphBounds, double scale, double offsetX, double offsetY)
    {
        var worldX = -_panX / _zoom;
        var worldY = -_panY / _zoom;
        var worldWidth = ClipBorder.Bounds.Width / _zoom;
        var worldHeight = ClipBorder.Bounds.Height / _zoom;

        var x = offsetX + (worldX - graphBounds.X) * scale;
        var y = offsetY + (worldY - graphBounds.Y) * scale;
        var width = worldWidth * scale;
        var height = worldHeight * scale;

        return new Rect(x, y, Math.Max(width, 4), Math.Max(height, 4));
    }

    private void UpdateNodeScreenScaling()
    {
        var inverseScale = Math.Clamp(1 / _zoom, 1, MaxScreenScale);
        foreach (var edgePath in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(GraphHost).OfType<Avalonia.Controls.Shapes.Path>()
            .Where(path => path.Tag is GraphEdge))
        {
            edgePath.StrokeThickness = EdgeHitboxWidth * inverseScale;
        }

        foreach (var nodeBorder in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(GraphHost).OfType<Border>()
            .Where(border => border.Classes.Contains("graph-node")))
        {
            if (nodeBorder.Tag is not GraphNode node)
                continue;

            var scale = inverseScale * (node.IsHovered ? 1.16 : 1);
            nodeBorder.Padding = new Thickness(10 * scale, 7 * scale);
            if (nodeBorder.Child is TextBlock label)
            {
                label.FontSize = Math.Clamp(NodeBaseFontSize * scale, NodeBaseFontSize, NodeBaseFontSize * MaxScreenScale);
                label.Text = GetNodeDisplayText(node, nodeBorder.IsPointerOver);
                nodeBorder.SetValue(Avalonia.Controls.ToolTip.TipProperty, node.Table.Name);
            }
        }
    }

    private string GetNodeDisplayText(GraphNode node, bool isPointerOver)
    {
        if (isPointerOver || node.IsHovered || node.IsSelected || node.IsHighlighted || node.IsCustomJoinSource)
            return node.Table.Name;

        return _zoom < AbbreviatedLabelZoom && node.Table.Name.Length > AbbreviatedLabelLength
            ? node.Table.Name[..AbbreviatedLabelLength] + "…"
            : node.Table.Name;
    }

    private void OnMinimapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(MinimapCanvas).Properties.IsLeftButtonPressed)
            return;

        _isMinimapDragging = true;
        e.Pointer.Capture(MinimapCanvas);
        MoveViewportFromMinimap(e.GetPosition(MinimapCanvas));
        e.Handled = true;
    }

    private void OnMinimapPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isMinimapDragging)
            MoveViewportFromMinimap(e.GetPosition(MinimapCanvas));
    }

    private void OnMinimapPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isMinimapDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void MoveViewportFromMinimap(Point minimapPoint)
    {
        if (DataContext is not MainViewModel vm || vm.NodeIndex.Count == 0)
            return;

        var graphBounds = ComputeGraphWorldBounds();
        if (graphBounds.Width <= 0 || graphBounds.Height <= 0)
            return;

        const double MinimapInset = 6;
        var minimapContentWidth = MinimapCanvas.Width - MinimapInset * 2;
        var minimapContentHeight = MinimapCanvas.Height - MinimapInset * 2;
        var scale = Math.Min(minimapContentWidth / graphBounds.Width, minimapContentHeight / graphBounds.Height);
        var offsetX = (minimapContentWidth - graphBounds.Width * scale) / 2;
        var offsetY = (minimapContentHeight - graphBounds.Height * scale) / 2;

        var graphX = (minimapPoint.X - MinimapInset - offsetX) / scale + graphBounds.X;
        var graphY = (minimapPoint.Y - MinimapInset - offsetY) / scale + graphBounds.Y;

        var worldCenterX = graphX;
        var worldCenterY = graphY;
        var viewportWidth = ClipBorder.Bounds.Width;
        var viewportHeight = ClipBorder.Bounds.Height;

        _panX = viewportWidth / 2 - worldCenterX * _zoom;
        _panY = viewportHeight / 2 - worldCenterY * _zoom;
        ClampPan();
        ApplyTransform();
    }
}
