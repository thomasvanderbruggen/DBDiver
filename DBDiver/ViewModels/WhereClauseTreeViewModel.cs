using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using DBDiver.Models;

namespace DBDiver.ViewModels;

public class WhereClauseTreeViewModel : ViewModelBase
{
    public interface IWhereClauseItem
    {
        bool HasConnector { get; set; }
    }

    private readonly Func<IReadOnlyList<WhereDropdownOption>> _getColumns;
    private readonly List<WhereClauseViewModel> _allConditions = [];
    private readonly List<WhereClauseGroupViewModel> _allGroups = [];
    private IReadOnlyList<WhereDropdownOption> _latestColumns = [];
    private readonly List<object> _selectedItems = [];
    private object? _dragItem;
    private bool _dragActive;
    private bool _isDropTargetValid;

    public event Action? StructureChanged;

    public WhereClauseTreeViewModel(Func<IReadOnlyList<WhereDropdownOption>> getColumns)
    {
        _getColumns = getColumns;
        RootItems.CollectionChanged += OnRootItemsChanged;
        AddConditionCommand = new RelayCommand(_ => AddCondition());
        AddGroupCommand = new RelayCommand(_ => AddGroup());
        DeleteCommand = new RelayCommand(parameter =>
        {
            if (parameter is WhereClauseViewModel condition)
                DeleteCondition(condition);
            else if (parameter is WhereClauseGroupViewModel group)
                DeleteGroup(group);
        });
        BeginDragCommand = new RelayCommand(parameter => { if (parameter is object item) BeginDrag(item); });
        SelectCommand = new RelayCommand(parameter => { if (parameter is object item) Select(item); });
    }

    public ObservableCollection<object> RootItems { get; } = [];

    public ICommand AddConditionCommand { get; }
    public ICommand AddGroupCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DragToRootCommand { get; } = null!;
    public ICommand BeginDragCommand { get; }
    public ICommand SelectCommand { get; }

    public IReadOnlyList<WhereClauseViewModel> Conditions => _allConditions;

    public bool HasInvalidItems
    {
        get
        {
            return _allConditions.Any(condition => condition.HasValidationIssue);
        }
    }

    public void RefreshColumns(IReadOnlyList<WhereDropdownOption> columns)
    {
        _latestColumns = columns;
        foreach (var condition in _allConditions)
            condition.UpdateColumns(columns);
    }

    public void BeginDrag(object item)
    {
        if (item is WhereClauseGroupViewModel group && IsDescendant(group, group))
            return;

        _dragItem = item;
        _dragActive = true;
    }

    public void DragOver(object target)
    {
        if (!_dragActive || _dragItem is null)
            return;

        bool invalidTarget = _dragItem is WhereClauseGroupViewModel draggedGroup && IsDescendant(draggedGroup, target);
        _isDropTargetValid = !ReferenceEquals(_dragItem, target) && !invalidTarget;
        SetDropTarget(target, _isDropTargetValid);
    }

    public void CompleteDrag(object target)
    {
        if (!_dragActive)
            return;

        _dragActive = false;
        SetDropTarget(target, false);

        if (target is null)
        {
            _dragItem = null;
            _isDropTargetValid = false;
            return;
        }

        if (_dragItem is null || !_isDropTargetValid)
            return;

        var moved = _dragItem;
        var movedCondition = moved as WhereClauseViewModel;
        _dragItem = null;
        _isDropTargetValid = false;

        if (target is WhereClauseGroupViewModel group)
        {
            if (movedCondition is not null)
                MoveIntoGroup(movedCondition, group);
            else if (moved is WhereClauseGroupViewModel movedGroup)
                MoveGroupIntoGroup(movedGroup, group);
        }
        else if (target is WhereClauseViewModel targetCondition && !ReferenceEquals(targetCondition, moved))
        {
            if (movedCondition is not null)
            {
                CreateGroup(new[] { targetCondition, movedCondition });
            }
            else if (moved is WhereClauseGroupViewModel movedGroup)
            {
                var parentGroup = FindParentGroup(RootItems, targetCondition);
                if (parentGroup is not null)
                    MoveGroupIntoGroup(movedGroup, parentGroup);
                else
                    MoveToRoot(movedGroup);
            }
        }
        else
        {
            if (moved is WhereClauseViewModel movedRootCondition)
                MoveToRoot(movedRootCondition);
            else
                MoveToRoot((WhereClauseGroupViewModel)moved);
        }
    }

    public void Select(object item)
    {
        _selectedItems.Clear();
        _selectedItems.Add(item);
        OnPropertyChanged(nameof(HasSelection));
    }

    public void BuildValidationState()
    {
        foreach (var condition in _allConditions)
            condition.BuildClause();
    }

    public bool HasSelection => _selectedItems.Count > 0;

    public void AddCondition()
    {
        var columns = _latestColumns.Count > 0 ? _latestColumns : _getColumns();
        var condition = new WhereClauseViewModel(columns, WhereClauseConnector.And, 0);
        RootItems.Add(condition);
    }

    public void AddGroup()
    {
        var group = new WhereClauseGroupViewModel(_getColumns(), WhereClauseConnector.And);
        RootItems.Add(group);
    }

    private void OnRootItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
            foreach (var item in e.NewItems)
                if (item is WhereClauseViewModel condition)
                    TrackCondition(condition);

        RefreshIndexes();
        RefreshGroupMetadata();
        RefreshConnectorStates(RootItems);
    }

    private void TrackCondition(WhereClauseViewModel condition)
    {
        if (_allConditions.Contains(condition))
            return;

        _allConditions.Add(condition);
        condition.ValidationIssueChanged += () =>
        {
            OnPropertyChanged(nameof(HasInvalidItems));
            StructureChanged?.Invoke();
        };
    }

    private void RefreshIndexes()
    {
        for (var index = 0; index < RootItems.Count; index++)
            if (RootItems[index] is WhereClauseViewModel condition)
                condition.RefreshIndexes(index);
    }

    private static void RefreshGroupDepths(IList<object> items, int groupDepth, int conditionDepth)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case WhereClauseGroupViewModel group:
                    group.Depth = groupDepth;
                    group.NotifyGroupDepthBrushChanged();
                    RefreshGroupDepths(group.Children, groupDepth + 1, group.Depth);
                    break;
                case WhereClauseViewModel condition:
                    condition.ParentGroupDepth = conditionDepth;
                    condition.NotifyGroupDepthBrushChanged();
                    break;
            }
        }
    }

    private void RefreshGroupMetadata()
    {
        _allGroups.Clear();
        RegisterGroups(RootItems);
        RefreshGroupNumbers(RootItems, 0);
        RefreshGroupDepths(RootItems, 0, 0);
    }

    private void RegisterGroups(IList<object> items)
    {
        foreach (var item in items)
        {
            if (item is not WhereClauseGroupViewModel group)
                continue;

            _allGroups.Add(group);
            group.Children.CollectionChanged -= OnGroupChildrenChanged;
            group.Children.CollectionChanged += OnGroupChildrenChanged;
            RegisterGroups(group.Children);
        }
    }

    private void OnGroupChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshGroupMetadata();

    private static void RefreshGroupNumbers(IList<object> items, int groupNumber)
    {
        foreach (var item in items)
        {
            if (item is not WhereClauseGroupViewModel group)
                continue;

            groupNumber++;
            group.GroupNumber = groupNumber;
            RefreshGroupNumbers(group.Children, groupNumber);
        }
    }

    public void RefreshGroupDepthBrushes()
    {
        RefreshGroupDepths(RootItems, 0, 0);
    }

    private static void RefreshConnectorStates(IList<object> items)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is not IWhereClauseItem item)
                continue;

            item.HasConnector = index > 0;

            if (items[index] is WhereClauseGroupViewModel group)
                RefreshConnectorStates(group.Children);
        }
    }

    private void DeleteCondition(WhereClauseViewModel condition)
    {
        if (RemoveChild(RootItems, condition))
        {
            _allConditions.Remove(condition);
            StructureChanged?.Invoke();
            OnPropertyChanged(nameof(HasInvalidItems));
        }
    }

    private void DeleteGroup(WhereClauseGroupViewModel group)
    {
        if (RemoveGroup(RootItems, group, out var children))
        {
        foreach (var child in children)
            if (child is WhereClauseViewModel condition)
                condition.Connector = WhereClauseConnector.And;
            else if (child is WhereClauseGroupViewModel childGroup)
                childGroup.Connector = WhereClauseConnector.And;

            StructureChanged?.Invoke();
            OnPropertyChanged(nameof(HasInvalidItems));
        }
    }

    private void MoveToRoot(WhereClauseViewModel condition)
    {
        if (!RemoveChild(RootItems, condition))
            return;

        condition.Connector = RootItems.Count == 0 ? WhereClauseConnector.And : condition.Connector;
        RootItems.Add(condition);
        StructureChanged?.Invoke();
    }

    private void MoveToRoot(WhereClauseGroupViewModel group)
    {
        if (IsDescendant(group, group) || !RemoveChild(RootItems, group))
            return;

        group.Connector = WhereClauseConnector.And;
        RootItems.Add(group);
        StructureChanged?.Invoke();
    }

    private void MoveIntoGroup(WhereClauseViewModel condition, WhereClauseGroupViewModel group)
    {
        if (ReferenceEquals(condition, group))
            return;

        if (!RemoveChild(RootItems, condition))
            return;

        condition.Connector = group.Children.Count == 0 ? WhereClauseConnector.And : condition.Connector;
        group.Children.Add(condition);
        StructureChanged?.Invoke();
    }

    private void MoveGroupIntoGroup(WhereClauseGroupViewModel movedGroup, WhereClauseGroupViewModel targetGroup)
    {
        if (ReferenceEquals(movedGroup, targetGroup))
            return;

        if (!RemoveChild(RootItems, movedGroup))
            return;

        movedGroup.Connector = targetGroup.Children.Count == 0 ? WhereClauseConnector.And : movedGroup.Connector;
        targetGroup.Children.Add(movedGroup);
        StructureChanged?.Invoke();
    }

    private static WhereClauseGroupViewModel? FindParentGroup(
        IList<object> items,
        WhereClauseViewModel condition)
    {
        foreach (var item in items)
        {
            if (item is not WhereClauseGroupViewModel group)
                continue;

            if (group.Children.Contains(condition))
                return group;

            var parentGroup = FindParentGroup(group.Children, condition);
            if (parentGroup is not null)
                return parentGroup;
        }

        return null;
    }

    private void SetDropTarget(object? target, bool isTarget)
    {
        foreach (var condition in _allConditions)
            condition.IsDropTarget = false;

        foreach (var group in EnumerateGroups(RootItems))
            group.IsDropTarget = false;

        if (isTarget && target is WhereClauseViewModel targetCondition)
            targetCondition.IsDropTarget = true;
        else if (isTarget && target is WhereClauseGroupViewModel targetGroup)
            targetGroup.IsDropTarget = true;
    }

    private static IEnumerable<WhereClauseGroupViewModel> EnumerateGroups(IList<object> items)
    {
        foreach (var item in items)
            if (item is WhereClauseGroupViewModel group)
            {
                yield return group;
                foreach (var child in EnumerateGroups(group.Children))
                    yield return child;
            }
    }

    private void CreateGroup(IReadOnlyList<WhereClauseViewModel> conditions)
    {
        var group = new WhereClauseGroupViewModel(_getColumns(), WhereClauseConnector.And);
        var targetIndex = RootItems.IndexOf(conditions[0]);

        foreach (var condition in conditions)
        {
            if (!RemoveChild(RootItems, condition))
                return;

            condition.Connector = group.Children.Count == 0 ? WhereClauseConnector.And : condition.Connector;
            group.Children.Add(condition);
        }

        if (targetIndex >= 0)
            RootItems.Insert(targetIndex, group);
        else
            RootItems.Add(group);
        StructureChanged?.Invoke();
    }

    private static bool RemoveChild(IList<object> items, object child)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], child))
            {
                items.RemoveAt(index);
                return true;
            }

            if (items[index] is WhereClauseGroupViewModel group && RemoveChild(group.Children, child))
                return true;
        }

        return false;
    }

    private static bool RemoveGroup(
        IList<object> items,
        WhereClauseGroupViewModel target,
        out IReadOnlyList<object> children)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], target))
            {
                children = target.Children.ToList();
                items.RemoveAt(index);
                return true;
            }

            if (items[index] is WhereClauseGroupViewModel group && RemoveGroup(group.Children, target, out children))
                return true;
        }

        children = [];
        return false;
    }

    private static bool IsDescendant(object container, object target) => container switch
    {
        WhereClauseGroupViewModel group when group.Children.Contains(target) => true,
        WhereClauseGroupViewModel group => group.Children.OfType<WhereClauseGroupViewModel>().Any(child => IsDescendant(child, target)),
        _ => false
    };
}

public class WhereClauseGroupViewModel : ViewModelBase,
    WhereClauseTreeViewModel.IWhereClauseItem
{
    private WhereClauseConnector _connector;
    private bool _isDropTarget;
    private bool _hasConnector;
    private int _depth;
    private int _groupNumber = 1;

    public WhereClauseGroupViewModel(IReadOnlyList<WhereDropdownOption> columns, WhereClauseConnector connector)
    {
        Columns = columns;
        _connector = connector;
        Children = [];
        Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasEmptyChildren));
    }

    public IReadOnlyList<WhereDropdownOption> Columns { get; }
    public ObservableCollection<object> Children { get; }
    public WhereClauseConnector Connector
    {
        get => _connector;
        set => SetProperty(ref _connector, value);
    }
    public bool IsAnd
    {
        get => Connector == WhereClauseConnector.And;
        set
        {
            if (value)
                Connector = WhereClauseConnector.And;
        }
    }
    public bool IsOr
    {
        get => Connector == WhereClauseConnector.Or;
        set
        {
            if (value)
                Connector = WhereClauseConnector.Or;
        }
    }
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }

    public bool HasConnector
    {
        get => _hasConnector;
        set => SetProperty(ref _hasConnector, value);
    }

    public string ConnectorGroup => $"WhereConnector_{GetHashCode()}";
    public bool HasEmptyChildren => Children.Count == 0;
    public int Depth
    {
        get => _depth;
        set => SetProperty(ref _depth, value);
    }
    public string GroupLabel => $"Group {GroupNumber}";
    public int GroupNumber
    {
        get => _groupNumber;
        set
        {
            if (SetProperty(ref _groupNumber, value))
                OnPropertyChanged(nameof(GroupLabel));
        }
    }
    public IBrush GroupDepthBrush => (Depth % 3) switch
    {
        0 => GetResourceBrush("QueryBuilderGroupDepth1Brush"),
        1 => GetResourceBrush("QueryBuilderGroupDepth2Brush"),
        _ => GetResourceBrush("QueryBuilderGroupDepth3Brush")
    };
    private static IBrush GetResourceBrush(string resourceKey)
        => Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var resource) == true
            && resource is IBrush brush ? brush : Brushes.Transparent;
    public void NotifyGroupDepthBrushChanged() => OnPropertyChanged(nameof(GroupDepthBrush));
}
