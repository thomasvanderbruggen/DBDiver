using System.Text.Json;
using System.Text.Json.Serialization;
using DBDiver.Models;

namespace DBDiver.Services;

public class NaturalLanguageQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly DbSchema _schema;
    private readonly QueryPathService _queryPathService;
    private readonly Dictionary<string, DbTable> _tablesByName;

    public NaturalLanguageQueryService(DbSchema schema, QueryPathService queryPathService)
    {
        _schema = schema;
        _queryPathService = queryPathService;
        _tablesByName = schema.Tables
            .Where(table => !string.IsNullOrWhiteSpace(table.Name))
            .GroupBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public NaturalLanguageQueryValidationResult Validate(NaturalLanguageQueryDto dto)
    {
        var result = new NaturalLanguageQueryValidationResult();

        if (dto.Rejected)
        {
            if (!string.IsNullOrWhiteSpace(dto.RejectionReason))
                result.Errors.Add($"The request was rejected: {dto.RejectionReason}");
            else
                result.Errors.Add("The request was rejected.");

            return result;
        }

        if (dto.Version != 1)
        {
            result.Errors.Add($"Unsupported query contract version {dto.Version}. Expected version 1.");
            return result;
        }

        if (dto.Select is null || dto.From is null)
        {
            result.Errors.Add("A valid query must contain select and from.");
            return result;
        }
        var referencedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in dto.From)
        {
            if (!TryGetTable(table, out var resolvedTable))
            {
                result.Errors.Add($"Unknown or ambiguous table '{table}' in FROM.");
                continue;
            }

            referencedTables.Add(resolvedTable.QualifiedName);
        }

        if (referencedTables.Count == 0)
            result.Errors.Add("No valid tables were provided in FROM.");

        if (referencedTables.Count > 1)
        {
            var connected = TryBuildJoinPath(referencedTables, out var joinRelationships, out var joinPathError);
            if (!connected)
            {
                result.Errors.Add(joinPathError);
            }
            else
            {
                result.JoinRelationships = joinRelationships;
            }
        }
        else
        {
            result.JoinRelationships = [];
        }

        var fromTables = new Dictionary<string, DbTable>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableName in dto.From)
            if (TryGetTable(tableName, out var table))
                fromTables[table.QualifiedName] = table;

        ValidateProjection(dto.Select, fromTables, result);
        ValidateColumnReferences(dto.GroupBy, fromTables, "GROUP BY", result);
        ValidateOrderBy(dto.OrderBy, fromTables, result);
        ValidateWhereClauses(dto.Where, fromTables, result);

        if (result.Errors.Count == 0)
            result.QueryModel = MapToQueryModel(dto);

        return result;
    }

    public NaturalLanguageQueryDto Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<NaturalLanguageQueryDto>(json, JsonOptions);
        return dto ?? throw new JsonException("The LLM returned an empty query object.");
    }

    private bool TryBuildJoinPath(HashSet<string> tables, out IReadOnlyList<DbRelationship> relationships, out string error)
    {
        relationships = [];
        error = string.Empty;

        if (tables.Count < 2)
        {
            return true;
        }

        var tableList = tables.OrderBy(table => table, StringComparer.OrdinalIgnoreCase).ToList();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tableList[0] };
        var remainingTables = new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);
        remainingTables.Remove(tableList[0]);
        var selectedRelationships = new List<DbRelationship>();

        while (remainingTables.Count > 0)
        {
            var path = FindShortestPathFromVisited(visited, remainingTables);
            if (path.Count == 0)
            {
                error = $"Unable to connect all requested tables. Unconnected tables: {string.Join(", ", remainingTables.OrderBy(table => table, StringComparer.OrdinalIgnoreCase))}.";
                return false;
            }

            selectedRelationships.AddRange(path);
            foreach (var relationship in path)
            {
                visited.Add(relationship.SourceTable);
                visited.Add(relationship.TargetTable);
            }

            remainingTables.RemoveWhere(visited.Contains);
        }

        relationships = selectedRelationships;
        return true;
    }

    private List<DbRelationship> FindShortestPathFromVisited(
        HashSet<string> visitedTables,
        HashSet<string> remainingTables)
    {
        var queue = new Queue<(string Table, List<DbRelationship> Path)>();
        var seen = new HashSet<string>(visitedTables, StringComparer.OrdinalIgnoreCase);

        foreach (var table in visitedTables)
            queue.Enqueue((table, []));

        while (queue.Count > 0)
        {
            var (table, path) = queue.Dequeue();

            foreach (var relationship in _schema.Relationships
                .Where(relationship => relationship.SourceTable.Equals(table, StringComparison.OrdinalIgnoreCase) ||
                                      relationship.TargetTable.Equals(table, StringComparison.OrdinalIgnoreCase))
                .OrderBy(relationship => relationship.SourceTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(relationship => relationship.SourceColumn, StringComparer.OrdinalIgnoreCase)
                .ThenBy(relationship => relationship.TargetTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(relationship => relationship.TargetColumn, StringComparer.OrdinalIgnoreCase))
            {
                var nextTable = relationship.SourceTable.Equals(table, StringComparison.OrdinalIgnoreCase)
                    ? relationship.TargetTable
                    : relationship.SourceTable;
                if (!seen.Add(nextTable))
                    continue;

                var nextPath = path.Append(relationship).ToList();
                if (remainingTables.Contains(nextTable))
                    return nextPath;

                queue.Enqueue((nextTable, nextPath));
            }
        }

        return [];
    }

    private void ValidateProjection(
        NaturalLanguageSelectDto select,
        IReadOnlyDictionary<string, DbTable> fromTables,
        NaturalLanguageQueryValidationResult result)
    {
        if (select.Columns.Count == 0)
        {
            result.Errors.Add("The SELECT clause must contain at least one column.");
            return;
        }

        foreach (var column in select.Columns)
        {
            if (!TryResolveReferencedTable(column.Table, fromTables, "SELECT", result, out var table))
            {
                continue;
            }

            if (column.Column == "*")
            {
                if (column.Aggregation != null && column.Aggregation != "COUNT")
                {
                    result.Errors.Add($"Aggregate '{column.Aggregation}' is not supported with '*'. Only COUNT(*) is supported.");
                }
                continue;
            }

            if (!TryResolveColumn(table, column.Column, out _))
            {
                result.Errors.Add($"Column '{column.Column}' does not exist on table '{table.QualifiedName}'.");
            }

            if (column.Aggregation != null &&
                column.Aggregation != "COUNT" &&
                column.Aggregation != "SUM" &&
                column.Aggregation != "AVG" &&
                column.Aggregation != "MIN" &&
                column.Aggregation != "MAX")
            {
                result.Errors.Add($"Unsupported aggregate function '{column.Aggregation}'.");
            }
        }
    }

    private void ValidateColumnReferences(
        IEnumerable<NaturalLanguageColumnReferenceDto> references,
        IReadOnlyDictionary<string, DbTable> fromTables,
        string clauseName,
        NaturalLanguageQueryValidationResult result)
    {
        foreach (var reference in references)
        {
            if (!TryResolveReferencedTable(reference.Table, fromTables, clauseName, result, out var table))
                continue;

            if (!TryResolveColumn(table, reference.Column, out _))
                result.Errors.Add($"Column '{reference.Column}' does not exist on table '{table.QualifiedName}' in {clauseName}.");
        }

    }

    private void ValidateOrderBy(
        IEnumerable<NaturalLanguageOrderByDto> references,
        IReadOnlyDictionary<string, DbTable> fromTables,
        NaturalLanguageQueryValidationResult result)
    {
        foreach (var reference in references)
        {
            if (!TryResolveReferencedTable(reference.Table, fromTables, "ORDER BY", result, out var table))
                continue;

            if (!TryResolveColumn(table, reference.Column, out _))
                result.Errors.Add($"Column '{reference.Column}' does not exist on table '{table.QualifiedName}' in ORDER BY.");

            if (reference.Aggregation != null &&
                reference.Aggregation != "COUNT" &&
                reference.Aggregation != "SUM" &&
                reference.Aggregation != "AVG" &&
                reference.Aggregation != "MIN" &&
                reference.Aggregation != "MAX")
            {
                result.Errors.Add($"Unsupported aggregate function '{reference.Aggregation}' in ORDER BY.");
            }
        }
    }

    private void ValidateWhereClauses(
        List<NaturalLanguageWhereNodeDto> clauses,
        IReadOnlyDictionary<string, DbTable> fromTables,
        NaturalLanguageQueryValidationResult result)
    {
        foreach (var clause in clauses)
        {
            switch (clause)
            {
                case NaturalLanguageWhereConditionDto condition:
                    if (!TryResolveReferencedTable(condition.Table, fromTables, "WHERE", result, out var table))
                    {
                        continue;
                    }

                    if (!TryResolveColumn(table, condition.Column, out _))
                    {
                        result.Errors.Add($"Column '{condition.Column}' does not exist on table '{table.QualifiedName}'.");
                    }

                    if (!TryGetWhereClauseOperator(condition.Comparator, out var @operator))
                    {
                        result.Errors.Add($"Unsupported comparator '{condition.Comparator}' in WHERE.");
                        continue;
                    }

                    if (!HasValidValueCount(@operator, condition.Values.Count))
                    {
                        result.Errors.Add($"Comparator '{condition.Comparator}' in WHERE has an invalid value count.");
                    }

                    break;

                case NaturalLanguageWhereGroupDto group:
                    if (group.Children.Count == 0)
                    {
                        result.Errors.Add("A WHERE group cannot be empty.");
                    }
                    else
                    {
                        ValidateWhereClauses(group.Children, fromTables, result);
                    }

                    break;
            }
        }
    }

    private QueryModel MapToQueryModel(NaturalLanguageQueryDto dto)
    {
        var query = new QueryModel();
        var tables = new Dictionary<string, QueryTable>(StringComparer.OrdinalIgnoreCase);
        var fromTables = dto.From ?? [];
        var select = dto.Select ?? new NaturalLanguageSelectDto { Columns = [] };

        foreach (var table in fromTables)
        {
            if (!TryGetTable(table, out var dbTable))
                continue;

            var queryTable = new QueryTable
            {
                Schema = dbTable.Schema,
                Name = dbTable.Name,
                Alias = dbTable.Name
            };
            tables[dbTable.QualifiedName] = queryTable;
            query.AddTable(queryTable);
        }

        var validation = new NaturalLanguageQueryValidationResult();
        var joinRelationships = new List<DbRelationship>();

        var referencedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in fromTables)
        {
            if (TryGetTable(table, out var resolvedTable))
                referencedTables.Add(resolvedTable.QualifiedName);
        }

        if (referencedTables.Count > 1 && TryBuildJoinPath(referencedTables, out var relationships, out _))
        {
            joinRelationships.AddRange(relationships);
        }

        foreach (var relationship in joinRelationships)
        {
            AddJoinTableIfMissing(relationship.SourceTable, query, tables);
            AddJoinTableIfMissing(relationship.TargetTable, query, tables);
        }

        foreach (var relationship in joinRelationships)
        {
            if (!tables.TryGetValue(relationship.SourceTable, out var sourceTable) ||
                !tables.TryGetValue(relationship.TargetTable, out var targetTable))
                continue;

            query.AddJoin(new QueryJoin
            {
                LeftTable = sourceTable,
                RightTable = targetTable,
                LeftColumn = relationship.SourceColumn,
                RightColumn = relationship.TargetColumn,
                JoinType = QueryJoinType.Left
            });
        }

        query.SetProjection(new QueryProjection
        {
            IsDistinct = select.Distinct,
            Items = select.Columns.Select(column =>
            {
                var table = ResolveTableName(column.Table);
                return new QueryProjectionItem
                {
                    Table = tables[table.QualifiedName],
                    Column = ResolveColumnName(table, column.Column),
                    Aggregation = ParseAggregateFunction(column.Aggregation),
                    Alias = column.Alias
                };
            }).ToList()
        });

        foreach (var groupBy in dto.GroupBy)
        {
            var table = ResolveTableName(groupBy.Table);
            query.AddGroupByItem(new QueryGroupByItem
            {
                Table = tables[table.QualifiedName],
                Column = ResolveColumnName(table, groupBy.Column)
            });
        }

        foreach (var orderBy in dto.OrderBy)
        {
            var table = ResolveTableName(orderBy.Table);
            query.AddOrderByItem(new QueryOrderByItem
            {
                Table = tables[table.QualifiedName],
                Column = ResolveColumnName(table, orderBy.Column),
                Aggregation = ParseAggregateFunction(orderBy.Aggregation),
                Direction = orderBy.Direction.Equals("DESC", StringComparison.OrdinalIgnoreCase) ? QueryOrderDirection.Descending : QueryOrderDirection.Ascending
            });
        }

        var whereNodes = BuildWhereNodes(dto.Where, tables);
        foreach (var node in whereNodes)
            query.AddWhereClause(node);

        return query;
    }

    private IReadOnlyList<WhereClauseNode> BuildWhereNodes(
        List<NaturalLanguageWhereNodeDto> clauses,
        IReadOnlyDictionary<string, QueryTable> queryTables)
    {
        var nodes = new List<WhereClauseNode>();

        for (var index = 0; index < clauses.Count; index++)
        {
            var clause = clauses[index];
            var connector = index > 0 ? ParseConnector(clause.Operator) : WhereClauseConnector.And;

            switch (clause)
            {
                case NaturalLanguageWhereConditionDto condition:
                    var dbTable = ResolveTableName(condition.Table);
                    var dbColumn = ResolveColumn(dbTable, condition.Column);
                    TryGetWhereClauseOperator(condition.Comparator, out var @operator);
                    nodes.Add(new QueryWhereClause
                    {
                        Connector = connector,
                        Table = queryTables[dbTable.QualifiedName],
                        Column = dbColumn.Name,
                        DataType = GetDataType(dbColumn),
                        Operator = @operator,
                        Value = condition.Values.FirstOrDefault(),
                        BetweenValue = condition.Values.Count > 1 ? condition.Values[1] : null
                    });
                    break;

                case NaturalLanguageWhereGroupDto group:
                    var children = BuildWhereNodes(group.Children, queryTables);
                    nodes.Add(new QueryWhereGroup
                    {
                        Connector = connector,
                        Children = children
                    });
                    break;
            }
        }

        return nodes;
    }

    private bool TryGetTable(string tableName, out DbTable table)
    {
        table = null!;

        if (string.IsNullOrWhiteSpace(tableName))
            return false;

        if (_tablesByName.TryGetValue(tableName, out var directMatch))
        {
            table = directMatch;
            return true;
        }

        var qualifiedMatch = _schema.Tables.FirstOrDefault(dbTable => dbTable.QualifiedName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        if (qualifiedMatch != null)
        {
            table = qualifiedMatch;
            return true;
        }

        return TryGetClosestTable(tableName, out table);
    }

    private bool TryResolveReferencedTable(
        string tableName,
        IReadOnlyDictionary<string, DbTable> fromTables,
        string clauseName,
        NaturalLanguageQueryValidationResult result,
        out DbTable table)
    {
        if (!TryGetTable(tableName, out table))
        {
            result.Errors.Add($"Unknown or ambiguous table '{tableName}' in {clauseName}.");
            return false;
        }

        if (!fromTables.ContainsKey(table.QualifiedName))
        {
            result.Errors.Add($"Table '{table.QualifiedName}' is referenced in {clauseName} but is not in FROM.");
            return false;
        }

        return true;
    }

    private bool TryGetClosestTable(string input, out DbTable table)
    {
        table = null!;

        var matches = _schema.Tables
            .Where(candidate => GetLevenshteinDistance(input, candidate.Name) == 1 ||
                                GetLevenshteinDistance(input, candidate.QualifiedName) == 1)
            .OrderBy(candidate => candidate.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matches.Count == 1)
            table = matches[0];

        return matches.Count == 1;
    }

    private bool TryResolveColumn(DbTable table, string columnName, out DbColumn column)
    {
        column = null!;

        if (string.IsNullOrWhiteSpace(columnName))
            return false;

        var directMatch = table.Columns.FirstOrDefault(dbColumn => dbColumn.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        if (directMatch is not null)
        {
            column = directMatch;
            return true;
        }

        var matches = table.Columns
            .Where(candidate => GetLevenshteinDistance(columnName, candidate.Name) == 1)
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matches.Count == 1)
            column = matches[0];

        return matches.Count == 1;
    }

    private DbColumn ResolveColumn(DbTable table, string columnName)
    {
        if (TryResolveColumn(table, columnName, out var column))
            return column;

        throw new InvalidOperationException($"Column '{columnName}' could not be resolved on table '{table.QualifiedName}'.");
    }

    private string ResolveColumnName(DbTable table, string columnName) => ResolveColumn(table, columnName).Name;

    private static int GetLevenshteinDistance(string first, string second)
    {
        first = first.ToLowerInvariant();
        second = second.ToLowerInvariant();
        var rows = first.Length + 1;
        var columns = second.Length + 1;
        var matrix = new int[rows, columns];

        for (var row = 0; row < rows; row++)
            matrix[row, 0] = row;
        for (var column = 0; column < columns; column++)
            matrix[0, column] = column;

        for (var row = 1; row < rows; row++)
        {
            for (var column = 1; column < columns; column++)
            {
                var cost = first[row - 1] == second[column - 1] ? 0 : 1;
                matrix[row, column] = Math.Min(
                    Math.Min(matrix[row - 1, column] + 1, matrix[row, column - 1] + 1),
                    matrix[row - 1, column - 1] + cost);
            }
        }

        return matrix[first.Length, second.Length];
    }

    private static bool HasValidValueCount(WhereClauseOperator @operator, int count) => @operator switch
    {
        WhereClauseOperator.Between => count == 2,
        WhereClauseOperator.IsNull or WhereClauseOperator.IsNotNull => count == 0,
        _ => count == 1
    };

    private static WhereClauseConnector ParseConnector(string? connector) =>
        connector?.Trim().ToUpperInvariant() == "OR" ? WhereClauseConnector.Or : WhereClauseConnector.And;

    private DbTable ResolveTableName(string tableName)
    {
        if (TryGetTable(tableName, out var table))
            return table;

        throw new InvalidOperationException($"Table '{tableName}' could not be resolved.");
    }

    private static void AddJoinTableIfMissing(
        string tableName,
        QueryModel query,
        Dictionary<string, QueryTable> queryTables)
    {
        if (queryTables.ContainsKey(tableName))
            return;

        var parts = tableName.Split('.', 2);
        var schema = parts.Length == 2 ? parts[0] : string.Empty;
        var name = parts.Length == 2 ? parts[1] : tableName;
        var queryTable = new QueryTable { Schema = schema, Name = name, Alias = name };
        queryTables[tableName] = queryTable;
        query.AddTable(queryTable);
    }

    private bool TryGetWhereClauseOperator(string comparator, out WhereClauseOperator @operator)
    {
        @operator = comparator.Trim().ToUpperInvariant() switch
        {
            "=" or "==" => WhereClauseOperator.Equals,
            "<" => WhereClauseOperator.LessThan,
            "<=" => WhereClauseOperator.LessThanOrEqual,
            ">" => WhereClauseOperator.GreaterThan,
            ">=" => WhereClauseOperator.GreaterThanOrEqual,
            "<>" or "!=" => WhereClauseOperator.NotEqual,
            "LIKE" => WhereClauseOperator.Like,
            "NOT LIKE" => WhereClauseOperator.NotLike,
            "BETWEEN" => WhereClauseOperator.Between,
            "IS NULL" => WhereClauseOperator.IsNull,
            "IS NOT NULL" => WhereClauseOperator.IsNotNull,
            _ => WhereClauseOperator.Equals
        };

        return comparator.Trim().ToUpperInvariant() is "=" or "==" or "<" or "<=" or ">" or ">=" or "<>" or "!=" or "LIKE" or "NOT LIKE" or "BETWEEN" or "IS NULL" or "IS NOT NULL";
    }

    private static WhereClauseDataType GetDataType(DbColumn column)
    {
        return column.DataType.ToLowerInvariant() switch
        {
            "bigint" or "decimal" or "double" or "float" or "int" or "money" or "numeric" or "real" or "smallint" or "smallmoney" or "tinyint" => WhereClauseDataType.Numeric,
            "char" or "varchar" or "nchar" or "nvarchar" => WhereClauseDataType.String,
            "text" or "ntext" => WhereClauseDataType.LegacyText,
            "binary" or "varbinary" => WhereClauseDataType.Binary,
            "rowversion" or "timestamp" => WhereClauseDataType.RowVersion,
            "date" => WhereClauseDataType.Date,
            "time" => WhereClauseDataType.Time,
            "datetime" or "datetime2" or "smalldatetime" => WhereClauseDataType.DateTime,
            "datetimeoffset" => WhereClauseDataType.DateTimeOffset,
            "bit" => WhereClauseDataType.Bit,
            "uniqueidentifier" => WhereClauseDataType.Guid,
            "hierarchyid" => WhereClauseDataType.HierarchyId,
            _ => WhereClauseDataType.Unsupported
        };
    }

    private static QueryAggregateFunction? ParseAggregateFunction(string? aggregation)
    {
        return aggregation?.ToUpperInvariant() switch
        {
            "COUNT" => QueryAggregateFunction.Count,
            "SUM" => QueryAggregateFunction.Sum,
            "AVG" => QueryAggregateFunction.Average,
            "MIN" => QueryAggregateFunction.Minimum,
            "MAX" => QueryAggregateFunction.Maximum,
            null => null,
            _ => null
        };
    }
}

public class NaturalLanguageQueryValidationResult
{
    public List<string> Errors { get; } = [];
    public QueryModel? QueryModel { get; set; }
    public IReadOnlyList<DbRelationship> JoinRelationships { get; set; } = [];

    public static NaturalLanguageQueryValidationResult Invalid(string error)
    {
        var result = new NaturalLanguageQueryValidationResult();
        result.Errors.Add(error);
        return result;
    }
}
