namespace DBDiver.Models;

public enum QueryJoinType
{
    Inner,
    Left,
    Right,
    Full
}

public enum WhereClauseDataType
{
    Numeric,
    String,
    LegacyText,
    Date,
    Time,
    DateTime,
    DateTimeOffset,
    Bit,
    Binary,
    RowVersion,
    Guid,
    HierarchyId,
    Unsupported
}

public enum WhereClauseOperator
{
    Equals,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
    NotEqual,
    Like,
    NotLike,
    Between,
    IsNull,
    IsNotNull
}

public enum WhereClauseConnector
{
    And,
    Or
}

public enum QueryAggregateFunction
{
    Count,
    Sum,
    Average,
    Minimum,
    Maximum
}

public enum QueryOrderDirection
{
    Ascending,
    Descending
}

public class QueryParameter
{
    public QueryParameter(string name, object value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public object Value { get; }
}

public abstract class WhereClauseNode
{
    public WhereClauseConnector Connector { get; init; }
}

public sealed class QueryWhereClause : WhereClauseNode
{
    public required QueryTable Table { get; init; }
    public required string Column { get; init; }
    public required WhereClauseDataType DataType { get; init; }
    public required WhereClauseOperator Operator { get; init; }
    public string? Value { get; init; }
    public string? BetweenValue { get; init; }
}

public sealed class QueryWhereGroup : WhereClauseNode
{
    public required IReadOnlyList<WhereClauseNode> Children { get; init; }
}

public class QueryTable
{
    public string Schema { get; init; } = string.Empty;
    public required string Name { get; init; }
    public required string Alias { get; init; }

    public string QualifiedName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}

public class QueryJoin
{
    public required QueryTable LeftTable { get; init; }
    public required QueryTable RightTable { get; init; }
    public required string LeftColumn { get; init; }
    public required string RightColumn { get; init; }
    public required QueryJoinType JoinType { get; set; }
}

public class QueryProjectionItem
{
    public required QueryTable Table { get; init; }
    public required string Column { get; init; }
    public QueryAggregateFunction? Aggregation { get; init; }
    public string? Alias { get; init; }
}

public class QueryProjection
{
    public bool IsDistinct { get; init; }
    public required IReadOnlyList<QueryProjectionItem> Items { get; init; }
}

public class QueryGroupByItem
{
    public required QueryTable Table { get; init; }
    public required string Column { get; init; }
}

public class QueryOrderByItem
{
    public required QueryTable Table { get; init; }
    public required string Column { get; init; }
    public QueryAggregateFunction? Aggregation { get; init; }
    public QueryOrderDirection Direction { get; init; } = QueryOrderDirection.Ascending;
}

public class QueryModel
{
    private readonly List<QueryTable> _tables = [];
    private readonly List<QueryJoin> _joins = [];
    private readonly List<WhereClauseNode> _whereClauses = [];

    public IReadOnlyList<QueryTable> Tables => _tables;
    public IReadOnlyList<QueryJoin> Joins => _joins;
    public IReadOnlyList<WhereClauseNode> WhereClauses => _whereClauses;
    public QueryProjection? Projection { get; private set; }
    private readonly List<QueryGroupByItem> _groupByItems = [];
    private readonly List<QueryOrderByItem> _orderByItems = [];

    public IReadOnlyList<QueryGroupByItem> GroupByItems => _groupByItems;
    public IReadOnlyList<QueryOrderByItem> OrderByItems => _orderByItems;

    public void AddTable(QueryTable table)
    {
        _tables.Add(table);
    }

    public void AddJoin(QueryJoin join)
    {
        _joins.Add(join);
    }

    public void AddWhereClause(WhereClauseNode clause)
    {
        _whereClauses.Add(clause);
    }

    public void ClearWhereClauses()
    {
        _whereClauses.Clear();
    }

    public void SetProjection(QueryProjection projection)
    {
        Projection = projection;
    }

    public void AddGroupByItem(QueryGroupByItem item)
    {
        _groupByItems.Add(item);
    }

    public void AddOrderByItem(QueryOrderByItem item)
    {
        _orderByItems.Add(item);
    }
}
