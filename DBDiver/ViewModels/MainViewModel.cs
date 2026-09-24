using System.Collections.ObjectModel;
using System.Reflection.Metadata.Ecma335;
using System.Windows.Input;
using DBDiver.Views;
using DBDiver.Models;
using DBDiver.Services;
using Serilog;

namespace DBDiver.ViewModels;

public class MainViewModel : ViewModelBase
{
    public static readonly Avalonia.Data.Converters.FuncValueConverter<int, bool> HopOneConverter = new(value => value >= 1);
    public static readonly Avalonia.Data.Converters.FuncValueConverter<int, bool> HopTwoConverter = new(value => value >= 2);
    public static readonly Avalonia.Data.Converters.FuncValueConverter<int, bool> HopThreeConverter = new(value => value >= 3);

    private readonly ISchemaService _schemaService;
    private readonly GraphLayoutService _graphLayout = new();
    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _appSettings;
    private readonly QueryPathService _queryPathService = new();
    private readonly SqlServerQueryRenderer _queryRenderer = new();
    private QueryModel? _activeQuery;
    private string _statusText = "Ready";
    private DbSchema? _currentSchema;
    private bool _isSchemaLoaded;
    public AppSettings CurrentSettings => _appSettings;

    public MainViewModel(ISchemaService schemaService)
    {
        _schemaService = schemaService;
        _appSettings = _settingsService.Load();
        LoadSchemaCommand = new RelayCommand(_ => LoadSchemaFromFile());
        SaveSchemaCommand = new RelayCommand(_ => SaveSchemaToFile());
        ZoomInCommand = new RelayCommand(_ => ZoomIn());
        ZoomOutCommand = new RelayCommand(_ => ZoomOut());
        FitGraphCommand = new RelayCommand(_ => FitGraph());
        ResetViewCommand = new RelayCommand(_ => ResetView());
        IncreaseHopsCommand = new RelayCommand(_ => MaxHopDepth++);
        DecreaseHopsCommand = new RelayCommand(_ => MaxHopDepth--);
        StartCustomJoinCommand = new RelayCommand(node =>
        {
            if (node is GraphNode graphNode)
                BeginCustomJoin(graphNode);
        });
        CancelCustomJoinCommand = new RelayCommand(_ => CancelCustomJoin());
    }

    public ObservableCollection<DbTable> Tables { get; } = [];
    public ObservableCollection<DbRelationship> Relationships { get; } = [];
    public ObservableCollection<GraphNode> GraphNodes { get; } = [];
    public ObservableCollection<GraphEdge> GraphEdges { get; } = [];
    public ObservableCollection<DbTable> IsolatedTables { get; } = [];
    public Dictionary<string, GraphNode> NodeIndex { get; } = new(StringComparer.OrdinalIgnoreCase);
    public event Action? GraphLayoutCompleted;
    public event Action<GraphNode>? SelectionChanged;
    public event Action? ViewportChanged;
    public event Action<bool>? ZoomRequested;
    public event Action<bool>? FitGraphRequested;
    public event Action? ViewportResetRequested;
    public event Action? HopDepthChanged;
    public event Action? QueryFinished;
    public event Action<IReadOnlyList<IReadOnlyList<DbRelationship>>, Action<IReadOnlyList<DbRelationship>>>? RelationshipChoiceRequested;

    private GraphNode? _customJoinSource;
    public GraphNode? CustomJoinSource
    {
        get => _customJoinSource;
        private set
        {
            _customJoinSource = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAwaitingCustomJoinTarget));
        }
    }

    private GraphNode? _joinPathSourceNode; 
    public GraphNode? JoinPathSourceNode
    {
        get => _joinPathSourceNode;
        private set
        {
            _joinPathSourceNode = value; 
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAwaitingJoinPathTarget));
        }
    }

    private string? _joinPathSearchText;

    public string? JoinPathSearchText
    {
        get => _joinPathSearchText;
        set
        {
            _joinPathSearchText = value;  
            OnPropertyChanged();
            UpdateJoinPathSearchResults(); 
        }
    }

    private readonly ObservableCollection<GraphNode> _joinPathSearchResults = [];
    
    public ObservableCollection<GraphNode> JoinPathSearchResults
    {
        get => _joinPathSearchResults;

    }

    private GraphNode? _selectedJoinPathTarget; 
    public GraphNode? SelectedJoinPathTarget
    {
        get => _selectedJoinPathTarget;
        private set
        {
            _selectedJoinPathTarget = value;
            OnPropertyChanged();
            CompleteJoinPathSearch(); 
        }
    }

    public void CompleteJoinPathSearch()
    {
        
        var sourceNode = JoinPathSourceNode;
        var endNode = SelectedJoinPathTarget; 
        if (endNode is null || sourceNode is null || string.Equals(sourceNode.Table.Name, endNode.Table.Name, StringComparison.OrdinalIgnoreCase) || _currentSchema is null)
        {
            // if (IsQueryBuilding) StatusText = "Query Building already started, please close query builder and search again"; 
            return; 
        }
        var joinPathDBRelationships = _queryPathService.FindShortestPaths(_currentSchema.Relationships,
            [sourceNode.Table.Name], endNode.Table.Name).FirstOrDefault();
        if (joinPathDBRelationships is null)
        {
            StatusText = "No join path found"; 
            return;
        }
        StatusText = $"Found Join Path from {sourceNode.Table.Name} to {endNode.Table.Name}"; 
        
        StartQueryFromFoundPath(sourceNode, joinPathDBRelationships);
        SelectedJoinPathTarget = null;
        JoinPathSourceNode = null;
        IsJoinPathSearchOpen = false; 

    }

    public void StartQueryFromFoundPath(GraphNode startingNode, IReadOnlyList<DbRelationship> dbRelationships)
    {
        if (!IsQueryBuilding)
        {
            BeginQuery(startingNode);    
        }
        
        foreach (var rel in dbRelationships)
        {
            AddRelationshipToQuery(rel);
        }
    }

    private readonly ObservableCollection<GraphNode>? _joinPathHops = []; 
    
    public ObservableCollection<GraphNode> JoinPathHops
    {
        get => _joinPathHops;
    }

    private bool _isJoinPathSearchOpen; 
    
    public bool IsJoinPathSearchOpen
    {
        get => _isJoinPathSearchOpen;
        private set
        {
            _isJoinPathSearchOpen = value;
            OnPropertyChanged();
        }
    }
    
    
    public bool IsAwaitingCustomJoinTarget => CustomJoinSource is not null;

    public bool IsAwaitingJoinPathTarget => JoinPathSourceNode is not null;

    
    private string _generatedSql = string.Empty;
    public string GeneratedSql
    {
        get => _generatedSql;
        private set
        {
            _generatedSql = value;
            OnPropertyChanged();
        }
    }

    public bool IsQueryBuilding => _activeQuery is not null;

    private int _maxHopDepth = 3;
    public int MaxHopDepth
    {
        get => _maxHopDepth;
        set
        {
            if (value < 1)
                value = 1;

            if (_maxHopDepth == value)
                return;

            _maxHopDepth = value;
            OnPropertyChanged();
            HopDepthChanged?.Invoke();

            if (SelectedNode is not null)
                UpdateSelectionEmphasis(SelectedNode);
        }
    }

    private GraphNode? _selectedNode;
    public GraphNode? SelectedNode
    {
        get => _selectedNode;
        private set
        {
            var previous = _selectedNode;
            _selectedNode = value;
            UpdateSelectionEmphasis(previous);
            UpdateSelectionEmphasis(value);
        OnPropertyChanged();
        SelectionChanged?.Invoke(value!);
        ViewportChanged?.Invoke();
        }
    }

    private void UpdateSelectionEmphasis(GraphNode? node)
    {
        if (node is null)
        {
            foreach (var graphNode in GraphNodes)
            {
                graphNode.IsSelected = false;
                graphNode.IsHighlighted = false;
                graphNode.IsDimmed = false;
            }

            foreach (var edge in GraphEdges)
            {
                edge.IsHighlighted = false;
                edge.IsDimmed = false;
            }

            return;
        }

        var reachableNodes = CollectConnectedNodes(node, MaxHopDepth);
        var reachableEdges = GraphEdges
            .Where(edge => reachableNodes.ContainsKey(edge.Source) && reachableNodes.ContainsKey(edge.Target))
            .ToHashSet();

        foreach (var graphNode in GraphNodes)
        {
            graphNode.IsSelected = graphNode == node;
            graphNode.HopDepth = reachableNodes.TryGetValue(graphNode, out var depth) ? depth : 0;
            graphNode.IsHighlighted = reachableNodes.ContainsKey(graphNode);
            graphNode.IsDimmed = !reachableNodes.ContainsKey(graphNode);
        }

        foreach (var edge in GraphEdges)
        {
            edge.HopDepth = reachableEdges.Contains(edge) ? Math.Min(reachableNodes[edge.Source], reachableNodes[edge.Target]) : 0;
            edge.IsHighlighted = reachableEdges.Contains(edge);
            edge.IsDimmed = !reachableEdges.Contains(edge);
        }
    }

    private Dictionary<GraphNode, int> CollectConnectedNodes(GraphNode startNode, int maxDepth)
    {
        var adjacency = BuildNodeAdjacency();
        var visited = new Dictionary<GraphNode, int> { [startNode] = 0 };
        var queue = new Queue<GraphNode>();
        queue.Enqueue(startNode);

        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();
            var nextDepth = visited[currentNode] + 1;
            if (nextDepth > maxDepth)
                continue;

            foreach (var neighbor in adjacency[currentNode])
            {
                if (!visited.TryGetValue(neighbor, out var existingDepth) || nextDepth < existingDepth)
                {
                    visited[neighbor] = nextDepth;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited;
    }

    private Dictionary<GraphNode, List<GraphNode>> BuildNodeAdjacency()
    {
        var adjacency = GraphNodes.ToDictionary(node => node, _ => new List<GraphNode>());
        foreach (var edge in GraphEdges)
        {
            adjacency[edge.Source].Add(edge.Target);
            adjacency[edge.Target].Add(edge.Source);
        }

        return adjacency;
    }

    public ICommand LoadSchemaCommand { get; }
    public ICommand SaveSchemaCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand FitGraphCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand IncreaseHopsCommand { get; }
    public ICommand DecreaseHopsCommand { get; }
    public ICommand StartCustomJoinCommand { get; }
    public ICommand CancelCustomJoinCommand { get; }

    private void ZoomIn() => ZoomRequested?.Invoke(true);
    private void ZoomOut() => ZoomRequested?.Invoke(false);
    private void FitGraph() => FitGraphRequested?.Invoke(true);

    private void ResetView()
    {
        SelectedNode = null;
        ViewportResetRequested?.Invoke();
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsSchemaLoaded
    {
        get => _isSchemaLoaded;
        private set
        {
            _isSchemaLoaded = value;
            OnPropertyChanged();
            ((RelayCommand)SaveSchemaCommand).RaiseCanExecuteChanged();
        }
    }

    public void LoadSchemaFromFile()
    {
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(OwnerWindow);
        if (topLevel is null)
        {
            StatusText = "No window available for file dialog";
            return;
        }

        var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Schema CSV",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("CSV Files")
                {
                    Patterns = ["*.csv"]
                }
            ]
        };

        _ = LoadSchemaFromPickerAsync(topLevel.StorageProvider, dialog);
    }

    public void SaveSchemaToFile()
    {
        if (_currentSchema is null)
        {
            StatusText = "No schema loaded";
            return;
        }

        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(OwnerWindow);
        if (topLevel is null)
        {
            StatusText = "No window available for save dialog";
            return;
        }

        var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save Schema",
            DefaultExtension = "csv",
            FileTypeChoices =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("CSV Files")
                {
                    Patterns = ["*.csv"]
                }
            ],
            SuggestedFileName = "schema.csv"
        };

        _ = SaveSchemaFromPickerAsync(topLevel.StorageProvider, dialog);
    }

    private async Task SaveSchemaFromPickerAsync(Avalonia.Platform.Storage.IStorageProvider storageProvider,
        Avalonia.Platform.Storage.FilePickerSaveOptions options)
    {
        if (_currentSchema is null)
            return;

        var file = await storageProvider.SaveFilePickerAsync(options);
        if (file is null)
        {
            StatusText = "No save file selected";
            return;
        }

        var filePath = file.Path.AbsolutePath;
        if (string.IsNullOrEmpty(filePath))
        {
            StatusText = "Could not resolve save path";
            return;
        }

        try
        {
            _schemaService.SaveSchema(_currentSchema, filePath);
            StatusText = $"Saved {_currentSchema.Tables.Count} tables and {_currentSchema.Relationships.Count} relationships to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save schema to {FilePath}", filePath);
            StatusText = $"Error saving schema: {ex.Message}";
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        _appSettings.Theme = settings.Theme;
        _appSettings.Layout = settings.Layout;
        _appSettings.QueryBuilder = settings.QueryBuilder;
        ApplyTheme(settings.Theme);
        ApplyQueryBuilderColors(settings.QueryBuilder);
        if (!IsQueryBuilding && _currentSchema is not null)
        {
            ComputeGraphLayout(settings.Layout);
            GraphLayoutCompleted?.Invoke();
        }
    }

    public Avalonia.Controls.Window? OwnerWindow { get; set; }

    private async Task LoadSchemaFromPickerAsync(Avalonia.Platform.Storage.IStorageProvider storageProvider,
        Avalonia.Platform.Storage.FilePickerOpenOptions options)
    {
        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0)
        {
            StatusText = "No file selected";
            return;
        }

        var filePath = files[0].Path.AbsolutePath;
        if (string.IsNullOrEmpty(filePath))
        {
            StatusText = "Could not resolve file path";
            return;
        }

        try
        {
            var schema = _schemaService.LoadSchema(filePath);
            ApplySchema(schema);
            ComputeGraphLayout();
            IsSchemaLoaded = true;
            StatusText = $"Loaded {schema.Tables.Count} tables, {schema.Relationships.Count} relationships, {IsolatedTables.Count} isolated from {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load schema from {FilePath}", filePath);
            StatusText = $"Error loading schema: {ex.Message}";
        }
    }

    private void ApplyTheme(GraphThemeSettings theme)
    {
        SetBrush("AppBackgroundBrush", theme.AppBackground);
        SetBrush("ToolbarTextBrush", theme.ToolbarText);
        SetBrush("ToolbarButtonBackgroundBrush", theme.ToolbarButtonBackground);
        SetBrush("ToolbarButtonForegroundBrush", theme.ToolbarButtonForeground);
        SetBrush("ToolbarButtonBorderBrush", theme.ToolbarButtonBorder);
        SetBrush("ToolbarButtonHoverBackgroundBrush", theme.ToolbarButtonHoverBackground);
        SetBrush("ToolbarButtonHoverBorderBrush", theme.ToolbarButtonHoverBorder);
        SetBrush("GraphBackgroundBrush", theme.GraphBackground);
        SetBrush("GraphNodeBackgroundBrush", theme.GraphNodeBackground);
        SetBrush("GraphNodeBorderBrush", theme.GraphNodeBorder);
        SetBrush("GraphNodeTextBrush", theme.GraphNodeText);
        SetBrush("GraphNodeHoverBackgroundBrush", theme.GraphNodeHoverBackground);
        SetBrush("GraphNodeHoverBorderBrush", theme.GraphNodeHoverBorder);
        SetBrush("GraphNodeHighlightBackgroundBrush", theme.GraphNodeHighlightBackground);
        SetBrush("GraphNodeHighlightBorderBrush", theme.GraphNodeHighlightBorder);
        SetBrush("GraphNodeSelectedBackgroundBrush", theme.GraphNodeSelectedBackground);
        SetBrush("GraphNodeSelectedBorderBrush", theme.GraphNodeSelectedBorder);
        SetBrush("GraphNodeHubBackgroundBrush", theme.GraphNodeHubBackground);
        SetBrush("GraphNodeHubBorderBrush", theme.GraphNodeHubBorder);
        SetBrush("GraphNodeCustomSourceBorderBrush", theme.GraphNodeCustomSourceBorder);
        SetBrush("GraphEdgeStrokeBrush", theme.GraphEdgeStroke);
        SetBrush("GraphEdgeHighlightStrokeBrush", theme.GraphEdgeHighlightStroke);
        SetBrush("GraphEdgeDimmedStrokeBrush", theme.GraphEdgeDimmedStroke);
        SetBrush("GraphEdgeCustomStrokeBrush", theme.GraphEdgeCustomStroke);
        SetBrush("StatusbarBackgroundBrush", theme.StatusbarBackground);
        SetBrush("StatusbarTextBrush", theme.StatusbarText);
        SetBrush("SidebarBackgroundBrush", theme.SidebarBackground);
        SetBrush("SidebarBorderBrush", theme.SidebarBorder);
        SetBrush("SidebarTextBrush", theme.SidebarText);
        SetBrush("SidebarItemBackgroundBrush", theme.SidebarItemBackground);
        SetBrush("SidebarItemBorderBrush", theme.SidebarItemBorder);
        SetBrush("SidebarItemTextBrush", theme.SidebarItemText);
        SetBrush("MinimapBackgroundBrush", theme.MinimapBackground);
        SetBrush("MinimapBorderBrush", theme.MinimapBorder);
    }

    private void ApplyQueryBuilderColors(QueryBuilderSettings settings)
    {
        SetBrush("QueryBuilderInnerJoinColorBrush", settings.InnerColor);
        SetBrush("QueryBuilderLeftJoinColorBrush", settings.LeftColor);
        SetBrush("QueryBuilderRightJoinColorBrush", settings.RightColor);
        SetBrush("QueryBuilderFullJoinColorBrush", settings.FullColor);
    }

    private void SetBrush(string resourceKey, string colorValue)
    {
        if (Avalonia.Application.Current?.Resources is { } resources && Avalonia.Media.Color.TryParse(colorValue, out var color))
            resources[resourceKey] = new Avalonia.Media.SolidColorBrush(color);
    }

    public void ApplySchema(DbSchema schema)
    {
        Tables.Clear();
        Relationships.Clear();

        foreach (var table in schema.Tables)
            Tables.Add(table);
        foreach (var rel in schema.Relationships)
            Relationships.Add(rel);
        _currentSchema = schema;
    }

    public void ComputeGraphLayout()
    {
        ComputeGraphLayout(_appSettings.Layout);
    }

    public void ComputeGraphLayout(GraphLayoutSettings layoutSettings)
    {
        if (_currentSchema is null) return;

        var nodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<GraphEdge>();

        var schemaTableNames = _currentSchema.Tables.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var connectedTableNames = _currentSchema.Relationships
            .SelectMany(r => new[] { r.SourceTable, r.TargetTable })
            .Where(schemaTableNames.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IsolatedTables.Clear();
        foreach (var table in _currentSchema.Tables.Where(t => !connectedTableNames.Contains(t.Name)))
            IsolatedTables.Add(table);

        foreach (var table in _currentSchema.Tables.Where(t => connectedTableNames.Contains(t.Name)))
        {
            var node = new GraphNode { Table = table };
            nodes[table.Name] = node;
        }

        foreach (var rel in _currentSchema.Relationships)
        {
            var sourceNode = nodes.GetValueOrDefault(rel.SourceTable);
            var targetNode = nodes.GetValueOrDefault(rel.TargetTable);
            if (sourceNode is null || targetNode is null) continue;

            var edge = new GraphEdge
            {
                Source = sourceNode,
                Target = targetNode,
                Relationship = rel
            };
            edge.Initialize();
            edges.Add(edge);
        }

            _graphLayout.ComputeLayout(_currentSchema, nodes, layoutSettings);

        NodeIndex.Clear();
        foreach (var (name, node) in nodes)
            NodeIndex[name] = node;

        GraphNodes.Clear();
        foreach (var node in nodes.Values)
            GraphNodes.Add(node);

        GraphEdges.Clear();
        foreach (var edge in edges)
            GraphEdges.Add(edge);

        OnPropertyChanged(nameof(GraphNodes));
        SelectedNode = null;
        foreach (var graphNode in GraphNodes)
        {
            graphNode.IsHovered = false;
            graphNode.IsHighlighted = false;
            graphNode.IsDimmed = false;
        }

        foreach (var edge in GraphEdges)
        {
            edge.IsHighlighted = false;
            edge.IsDimmed = false;
        }

        GraphLayoutCompleted?.Invoke();

        Log.Information("Applied schema with {TableCount} tables, {RelationshipCount} relationships, and {IsolatedTableCount} isolated tables",
            Tables.Count, Relationships.Count, IsolatedTables.Count);
    }

    public void ToggleNodeSelection(GraphNode node)
    {
        SelectedNode = ReferenceEquals(SelectedNode, node) ? null : node;
    }

    public void BeginCustomJoin(GraphNode sourceNode)
    {
        if (IsQueryBuilding)
            return;

        CustomJoinSource = sourceNode;
        sourceNode.IsCustomJoinSource = true;
        StatusText = $"Select the target table for the custom join from {sourceNode.Table.Name}.";
    }

    public void CancelCustomJoin()
    {
        if (CustomJoinSource is not null)
            StatusText = "Custom join canceled.";

        CustomJoinSource = null;
    }

    public void CancelQuery()
    {
        if (!IsQueryBuilding)
            return;

        ResetQuery();
        StatusText = "Query canceled.";
    }

    public void BeginQuery(GraphNode sourceNode)
    {
        if (_currentSchema is null || IsAwaitingCustomJoinTarget || IsQueryBuilding)
            return;

        _activeQuery = new QueryModel();
        _activeQuery.AddTable(new QueryTable
        {
            Name = sourceNode.Table.Name,
            Alias = GetAlias(0)
        });
        GeneratedSql = string.Empty;
        OnPropertyChanged(nameof(IsQueryBuilding));
        AssignQueryJoinsToEdges();
        StatusText = $"Building query from {sourceNode.Table.Name}. Select additional tables, then use Finish Query.";

        Log.Information("Started query from {TableName}", sourceNode.Table.Name);
    }

    private void UpdateJoinPathSearchResults()
    {
        var searchText = JoinPathSearchText?.Trim();
        JoinPathSearchResults.Clear();
        foreach (var table in GraphNodes)
        {
            if (table == JoinPathSourceNode) continue;
            if (string.IsNullOrEmpty(searchText) || table.Table.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                JoinPathSearchResults.Add(table);
            }
        }
    }

    public void FindTableFromStartingTable(GraphNode sourceNode)
    {
        if (_currentSchema is null || IsAwaitingCustomJoinTarget)
            return;
        JoinPathSourceNode = sourceNode;
        JoinPathSearchText = string.Empty;
        _joinPathHops?.Clear();
        IsJoinPathSearchOpen = true; 
        StatusText = $"Search for a table to join from {sourceNode.Table.Name}";
    }
    public void AddQueryTable(GraphNode targetNode)
    {
        if (_activeQuery is not { } query || _currentSchema is null)
            return;

        var targetTableName = targetNode.Table.Name;
        if (query.Tables.Any(table => table.Name.Equals(targetTableName, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = $"{targetTableName} is already part of the query.";
            return;
        }

        var shortestPaths = _queryPathService.FindShortestPaths(
            _currentSchema.Relationships,
            query.Tables.Select(table => table.Name).ToList(),
            targetTableName);

        if (shortestPaths.Count == 0)
        {
            StatusText = $"No path found to {targetTableName}.";
            return;
        }

        var candidates = shortestPaths
            .SelectMany(ExpandPathVariants)
            .GroupBy(path => string.Join("|", path.Select(relationship =>
                $"{relationship.SourceTable}.{relationship.SourceColumn}->{relationship.TargetTable}.{relationship.TargetColumn}")))
            .Select(group => group.First())
            .ToList();

        if (candidates.Count == 1)
        {
            AddQueryPath(candidates[0], targetTableName);
            return;
        }

        StatusText = $"Multiple join paths to {targetTableName}. Choose one.";
        Log.Information("Multiple join paths found to {TargetTable}: {Count}", targetTableName, candidates.Count);
        RelationshipChoiceRequested?.Invoke(candidates, candidate => AddQueryPath(candidate, targetTableName));
    }

    private static bool IsSamePair(DbRelationship relationship, string tableA, string tableB) =>
        (relationship.SourceTable.Equals(tableA, StringComparison.OrdinalIgnoreCase) &&
         relationship.TargetTable.Equals(tableB, StringComparison.OrdinalIgnoreCase)) ||
                (relationship.SourceTable.Equals(tableB, StringComparison.OrdinalIgnoreCase) &&
                 relationship.TargetTable.Equals(tableA, StringComparison.OrdinalIgnoreCase));

    private List<List<DbRelationship>> ExpandPathVariants(IReadOnlyList<DbRelationship> path)
    {
        IEnumerable<List<DbRelationship>> variants = [[]];

        foreach (var relationship in path)
        {
            var siblings = _currentSchema!.Relationships
                .Where(r => IsSamePair(r, relationship.SourceTable, relationship.TargetTable))
                .OrderBy(r => r.SourceTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.SourceColumn, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.TargetColumn, StringComparer.OrdinalIgnoreCase);

            variants = variants.SelectMany(partial => siblings.Select(sibling => partial.Append(sibling).ToList()));
        }

        return variants.ToList();
    }

    private void AddQueryPath(IReadOnlyList<DbRelationship> path, string targetTableName)
    {
        if (_activeQuery is null || _currentSchema is null)
            return;

        foreach (var relationship in path)
            AddRelationshipToQuery(relationship);

        StatusText = $"Added {targetTableName} through {path.Count} relationship{(path.Count == 1 ? string.Empty : "s")}.";
        Log.Information("Added query path to {TargetTable} with {RelationshipCount} relationships",
            targetTableName, path.Count);
    }

    public void FinishQuery()
    {
        if (_activeQuery is not { } query)
            return;

        GeneratedSql = _queryRenderer.Render(query);
        StatusText = "Query finished. Copy the SQL, then reset to build another query.";
        UpdateFinishedQueryEmphasis();

        Log.Information("Finished query with {TableCount} tables and {JoinCount} joins",
            query.Tables.Count, query.Joins.Count);
        QueryFinished?.Invoke();
    }

    private void UpdateFinishedQueryEmphasis()
    {
        var queryTableNames = _activeQuery!.Tables
            .Select(table => table.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var node in GraphNodes)
        {
            node.IsHighlighted = queryTableNames.Contains(node.Table.Name);
            node.IsDimmed = !node.IsHighlighted;
            node.HopDepth = 0;
        }

        foreach (var edge in GraphEdges)
        {
            edge.IsHighlighted = edge.IsQueryEdge;
            edge.IsDimmed = !edge.IsQueryEdge;
            edge.HopDepth = 0;
        }
    }

    public void ResetQuery()
    {
        if (_activeQuery is null && GeneratedSql.Length == 0)
            return;

        _activeQuery = null;
        GeneratedSql = string.Empty;
        OnPropertyChanged(nameof(IsQueryBuilding));
        AssignQueryJoinsToEdges();
        ClearQueryEmphasis();
        StatusText = "Query reset.";

        Log.Information("Reset query state");
    }

    public bool IsActiveQueryTable(string tableName) =>
        _activeQuery?.Tables.Any(table => table.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase)) == true;

    public void CycleJoinType(GraphEdge edge)
    {
        if (!IsQueryBuilding || GeneratedSql.Length > 0 || edge.QueryJoin is not { } join)
            return;

        join.JoinType = join.JoinType switch
        {
            QueryJoinType.Left => QueryJoinType.Inner,
            QueryJoinType.Inner => QueryJoinType.Right,
            QueryJoinType.Right => QueryJoinType.Full,
            QueryJoinType.Full => QueryJoinType.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(edge), join.JoinType, "Unsupported join type")
        };

        edge.RefreshJoinType();
        StatusText = $"{edge.Source.Table.Name} → {edge.Target.Table.Name} set to {join.JoinType.ToString().ToUpperInvariant()} JOIN.";
    }

    private QueryJoinType MapDefaultJoinType() => _appSettings.QueryBuilder.DefaultJoinType switch
    {
        Models.QueryBuilderJoinType.Inner => QueryJoinType.Inner,
        Models.QueryBuilderJoinType.Right => QueryJoinType.Right,
        Models.QueryBuilderJoinType.Full => QueryJoinType.Full,
        _ => QueryJoinType.Left
    };

    private void AssignQueryJoinsToEdges()
    {
        foreach (var edge in GraphEdges)
        {
            edge.QueryJoin = null;
            edge.IsQueryActive = _activeQuery is not null;
        }

        if (_activeQuery is not { } query)
            return;

        foreach (var join in query.Joins)
        {
            var leftName = join.LeftTable.Name;
            var rightName = join.RightTable.Name;
            var edge = GraphEdges.FirstOrDefault(candidate =>
            {
                var forward = candidate.Source.Table.Name.Equals(leftName, StringComparison.OrdinalIgnoreCase) &&
                              candidate.Target.Table.Name.Equals(rightName, StringComparison.OrdinalIgnoreCase) &&
                              candidate.Relationship.SourceColumn.Equals(join.LeftColumn, StringComparison.OrdinalIgnoreCase) &&
                              candidate.Relationship.TargetColumn.Equals(join.RightColumn, StringComparison.OrdinalIgnoreCase);
                var reverse = candidate.Source.Table.Name.Equals(rightName, StringComparison.OrdinalIgnoreCase) &&
                              candidate.Target.Table.Name.Equals(leftName, StringComparison.OrdinalIgnoreCase) &&
                              candidate.Relationship.SourceColumn.Equals(join.RightColumn, StringComparison.OrdinalIgnoreCase) &&
                              candidate.Relationship.TargetColumn.Equals(join.LeftColumn, StringComparison.OrdinalIgnoreCase);
                return forward || reverse;
            });

            if (edge is not null)
                edge.QueryJoin = join;
            else
                Log.Warning("Query join could not be matched to a graph edge: {LeftTable}.{LeftColumn} -> {RightTable}.{RightColumn}",
                    leftName, join.LeftColumn, rightName, join.RightColumn);
        }
    }

    public void AddRelationshipToQuery(DbRelationship relationship)
    {
        if (_activeQuery is not { } query || _currentSchema is null)
            return;

        var existingTable = query.Tables.FirstOrDefault(table =>
            table.Name.Equals(relationship.SourceTable, StringComparison.OrdinalIgnoreCase) ||
            table.Name.Equals(relationship.TargetTable, StringComparison.OrdinalIgnoreCase));

        if (existingTable is null)
        {
            Log.Warning("Join relationship does not connect to any existing query table: {SourceTable} -> {TargetTable}",
                relationship.SourceTable, relationship.TargetTable);
            return;
        }

        var currentIsSource = existingTable.Name.Equals(relationship.SourceTable, StringComparison.OrdinalIgnoreCase);
        var neighborName = currentIsSource ? relationship.TargetTable : relationship.SourceTable;

        var neighborTable = query.Tables.FirstOrDefault(table =>
            table.Name.Equals(neighborName, StringComparison.OrdinalIgnoreCase));

        if (neighborTable is null)
        {
            neighborTable = new QueryTable
            {
                Name = neighborName,
                Alias = GetAlias(query.Tables.Count)
            };
            query.AddTable(neighborTable);
        }

        query.AddJoin(new QueryJoin
        {
            LeftTable = existingTable,
            RightTable = neighborTable,
            LeftColumn = currentIsSource ? relationship.SourceColumn : relationship.TargetColumn,
            RightColumn = currentIsSource ? relationship.TargetColumn : relationship.SourceColumn,
            JoinType = MapDefaultJoinType()
        });

        AssignQueryJoinsToEdges();
        UpdateQueryEmphasis();
        StatusText = $"Added {neighborName} via {relationship.SourceColumn} → {relationship.TargetColumn}.";
    }

    private static string GetAlias(int index) => $"t{index}";

    private void ClearQueryEmphasis()
    {
        foreach (var node in GraphNodes)
        {
            node.IsHighlighted = false;
            node.IsDimmed = false;
        }

        foreach (var edge in GraphEdges)
        {
            edge.IsHighlighted = false;
            edge.IsDimmed = false;
        }

        if (SelectedNode is { } selectedNode)
            UpdateSelectionEmphasis(selectedNode);
    }

    private void UpdateQueryEmphasis()
    {
        if (_activeQuery is null)
            return;

        var queryTableNames = _activeQuery.Tables
            .Select(table => table.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var adjacency = BuildNodeAdjacency();
        var nearbyNodes = new HashSet<GraphNode>();
        foreach (var node in GraphNodes.Where(node => queryTableNames.Contains(node.Table.Name)))
        {
            foreach (var neighbor in adjacency[node])
                nearbyNodes.Add(neighbor);
        }

        foreach (var node in GraphNodes)
        {
            var isQueryTable = queryTableNames.Contains(node.Table.Name);
            node.IsHighlighted = isQueryTable || nearbyNodes.Contains(node);
            node.IsDimmed = !node.IsHighlighted;
            node.HopDepth = !isQueryTable && nearbyNodes.Contains(node) ? 1 : 0;
        }

        foreach (var edge in GraphEdges)
        {
            var isQueryEdge = edge.IsQueryEdge;
            var isNearbyEdge = !isQueryEdge &&
                               (queryTableNames.Contains(edge.Source.Table.Name) || nearbyNodes.Contains(edge.Source)) &&
                               (queryTableNames.Contains(edge.Target.Table.Name) || nearbyNodes.Contains(edge.Target)) &&
                               (queryTableNames.Contains(edge.Source.Table.Name) || queryTableNames.Contains(edge.Target.Table.Name));
            edge.IsHighlighted = isQueryEdge || isNearbyEdge;
            edge.IsDimmed = !(isQueryEdge || isNearbyEdge);
            edge.HopDepth = isNearbyEdge ? 2 : 0;
            edge.IsQueryActive = true;
        }
    }

    public async Task CompleteCustomJoinTargetAsync(GraphNode targetNode)
    {
        if (CustomJoinSource is not { } sourceNode || ReferenceEquals(sourceNode, targetNode))
            return;

        var sourceTable = sourceNode.Table;
        var targetTable = targetNode.Table;

        var dialog = new CustomJoinDialog(sourceTable, targetTable);
        var sourceWindow = OwnerWindow;
        if (sourceWindow is not null)
        {
            try
            {
                var confirmed = await dialog.ShowDialog<bool>(sourceWindow);

                if (confirmed)
                {
                    AddCustomRelationship(sourceNode, targetNode, dialog.SourceColumn, dialog.TargetColumn);
                }
                else
                {
                    StatusText = "Custom join canceled.";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error creating custom join: {ex.Message}";
            }
            finally
            {
                sourceNode.IsCustomJoinSource = false;
                CustomJoinSource = null;
            }
        }
        else
        {
            StatusText = "No window available for custom join dialog";
            sourceNode.IsCustomJoinSource = false;
            CustomJoinSource = null;
        }
    }

    public void AddCustomRelationship(GraphNode sourceNode, GraphNode targetNode, string sourceColumn, string targetColumn)
    {
        if (_currentSchema is null)
            return;

        var relationship = new DbRelationship
        {
            SourceTable = sourceNode.Table.Name,
            SourceColumn = sourceColumn,
            TargetTable = targetNode.Table.Name,
            TargetColumn = targetColumn,
            IsCustom = true
        };

        _currentSchema.Relationships.Add(relationship);

        var edge = new GraphEdge
        {
            Source = sourceNode,
            Target = targetNode,
            Relationship = relationship
        };
        edge.Initialize();
        GraphEdges.Add(edge);

        var selected = SelectedNode;
        SelectedNode = null;
        SelectedNode = selected;

        StatusText = $"Added custom join {sourceNode.Table.Name}.{sourceColumn} -> {targetNode.Table.Name}.{targetColumn}";
        CustomJoinSource = null;

        Log.Information(
            "Added custom relationship {SourceTable}.{SourceColumn} -> {TargetTable}.{TargetColumn}",
            relationship.SourceTable, relationship.SourceColumn, relationship.TargetTable, relationship.TargetColumn);
    }

    public void SetNodeHover(GraphNode node, bool isHovered)
    {
        node.IsHovered = isHovered;
    }
}
