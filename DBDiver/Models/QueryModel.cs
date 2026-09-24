namespace DBDiver.Models;

public enum QueryJoinType
{
    Inner,
    Left,
    Right,
    Full
}

public class QueryTable
{
    public required string Name { get; init; }
    public required string Alias { get; init; }
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

    public IReadOnlyList<QueryTable> Tables => _tables;
    public IReadOnlyList<QueryJoin> Joins => _joins;

    public void AddTable(QueryTable table)
    {
        _tables.Add(table);
    }

    public void AddJoin(QueryJoin join)
    {
        _joins.Add(join);
    }
}
