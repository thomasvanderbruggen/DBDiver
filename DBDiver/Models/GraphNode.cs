using System.ComponentModel;
using DBDiver.ViewModels;

namespace DBDiver.Models;

public class GraphNode : ViewModelBase
{
    private double _x;
    private double _y;
    private double _width = 160;
    private double _height = 64;
    private bool _isSelected;
    private bool _isHovered;
    private bool _isHighlighted;
    private bool _isDimmed;
    private bool _isHub;
    private int _hopDepth;
    private bool _isCustomJoinSource;

    public DbTable Table { get; set; } = null!;

    public double X
    {
        get => _x;
        set
        {
            _x = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Position));
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            _y = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Position));
        }
    }

    public double VelocityX { get; set; }
    public double VelocityY { get; set; }

    public double Width
    {
        get => _width;
        set
        {
            _width = value;
            OnPropertyChanged();
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            _height = value;
            OnPropertyChanged();
        }
    }

    public Avalonia.Thickness Position => new(X, Y, 0, 0);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            _isHovered = value;
            OnPropertyChanged();
        }
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            _isHighlighted = value;
            OnPropertyChanged();
        }
    }

    public bool IsDimmed
    {
        get => _isDimmed;
        set
        {
            _isDimmed = value;
            OnPropertyChanged();
        }
    }

    public bool IsHub
    {
        get => _isHub;
        set
        {
            _isHub = value;
            OnPropertyChanged();
        }
    }

    public int HopDepth
    {
        get => _hopDepth;
        set
        {
            _hopDepth = value;
            OnPropertyChanged();
        }
    }

    public bool IsCustomJoinSource
    {
        get => _isCustomJoinSource;
        set
        {
            _isCustomJoinSource = value;
            OnPropertyChanged();
        }
    }
}

public class GraphEdge : ViewModelBase
{
    private bool _isHighlighted;
    private bool _isDimmed;
    private int _hopDepth;
    private QueryJoin? _queryJoin;

    public GraphNode Source { get; set; } = null!;
    public GraphNode Target { get; set; } = null!;
    public DbRelationship Relationship { get; set; } = null!;

    public bool IsCustom => Relationship.IsCustom;

    public QueryJoin? QueryJoin
    {
        get => _queryJoin;
        set
        {
            _queryJoin = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsQueryEdge));
            OnPropertyChanged(nameof(JoinTypeName));
            OnPropertyChanged(nameof(IsInnerJoin));
            OnPropertyChanged(nameof(IsLeftJoin));
            OnPropertyChanged(nameof(IsRightJoin));
            OnPropertyChanged(nameof(IsFullJoin));
        }
    }

    public bool IsQueryEdge => QueryJoin is not null;
    private bool _isQueryActive;

    public bool IsQueryActive
    {
        get => _isQueryActive;
        set
        {
            _isQueryActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomQueryEdge));
        }
    }

    public string JoinTypeName => QueryJoin?.JoinType.ToString() ?? string.Empty;

    public bool IsInnerJoin => QueryJoin?.JoinType == QueryJoinType.Inner;
    public bool IsLeftJoin => QueryJoin?.JoinType == QueryJoinType.Left;
    public bool IsRightJoin => QueryJoin?.JoinType == QueryJoinType.Right;
    public bool IsFullJoin => QueryJoin?.JoinType == QueryJoinType.Full;

    public bool IsCustomQueryEdge => IsCustom && IsQueryActive;

    public Avalonia.Point StartPoint => new(Source.X, Source.Y);
    public Avalonia.Point EndPoint => new(Target.X, Target.Y);

    public void Initialize()
    {
        Source.PropertyChanged += OnEndpointPropertyChanged;
        Target.PropertyChanged += OnEndpointPropertyChanged;
    }

    private void OnEndpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GraphNode.X) or nameof(GraphNode.Y))
            OnEndpointChanged();
    }

    private void OnEndpointChanged()
    {
        OnPropertyChanged(nameof(StartPoint));
        OnPropertyChanged(nameof(EndPoint));
    }

    public void RefreshJoinType()
    {
        OnPropertyChanged(nameof(JoinTypeName));
        OnPropertyChanged(nameof(IsInnerJoin));
        OnPropertyChanged(nameof(IsLeftJoin));
        OnPropertyChanged(nameof(IsRightJoin));
        OnPropertyChanged(nameof(IsFullJoin));
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            _isHighlighted = value;
            OnPropertyChanged();
        }
    }

    public bool IsDimmed
    {
        get => _isDimmed;
        set
        {
            _isDimmed = value;
            OnPropertyChanged();
        }
    }

    public int HopDepth
    {
        get => _hopDepth;
        set
        {
            _hopDepth = value;
            OnPropertyChanged();
        }
    }
}
