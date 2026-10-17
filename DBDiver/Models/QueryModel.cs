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
    Date,
    Bit,
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

public class QueryWhereClause
{
    public required QueryTable Table { get; init; }
    public required string Column { get; init; }
    public required WhereClauseDataType DataType { get; init; }
    public required WhereClauseOperator Operator { get; init; }
    public string? Value { get; init; }
    public string? BetweenValue { get; init; }
    public WhereClauseConnector Connector { get; init; }
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

public class QueryModel
{
    private readonly List<QueryTable> _tables = [];
    private readonly List<QueryJoin> _joins = [];
    private readonly List<QueryWhereClause> _whereClauses = [];

    public IReadOnlyList<QueryTable> Tables => _tables;
    public IReadOnlyList<QueryJoin> Joins => _joins;
    public IReadOnlyList<QueryWhereClause> WhereClauses => _whereClauses;

    public void AddTable(QueryTable table)
    {
        _tables.Add(table);
    }

    public void AddJoin(QueryJoin join)
    {
        _joins.Add(join);
    }

    public void AddWhereClause(QueryWhereClause clause)
    {
        _whereClauses.Add(clause);
    }

    public void ClearWhereClauses()
    {
        _whereClauses.Clear();
    }
}
