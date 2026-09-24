using DBDiver.Models;

namespace DBDiver.Services;

public class QueryPathService
{
    public IReadOnlyList<IReadOnlyList<DbRelationship>> FindShortestPaths(
        IReadOnlyCollection<DbRelationship> relationships,
        IReadOnlyCollection<string> sourceTables,
        string targetTable)
    {
        if (relationships.Count == 0 || sourceTables.Count == 0 || string.IsNullOrWhiteSpace(targetTable))
            return [];

        var comparer = StringComparer.OrdinalIgnoreCase;
        var distance = new Dictionary<string, int>(comparer);
        var paths = new Dictionary<string, List<List<DbRelationship>>>(comparer);
        var queue = new Queue<string>();

        foreach (var sourceTable in sourceTables)
        {
            if (distance.ContainsKey(sourceTable))
                continue;

            distance[sourceTable] = 0;
            paths[sourceTable] = [[]];
            queue.Enqueue(sourceTable);
        }

        if (distance.ContainsKey(targetTable))
            return [];

        while (queue.Count > 0)
        {
            var tableName = queue.Dequeue();
            var currentDistance = distance[tableName];

            foreach (var relationship in GetAdjacentRelationships(relationships, tableName))
            {
                var nextTable = GetNeighbor(relationship, tableName);
                if (distance.TryGetValue(nextTable, out var nextDistance) && nextDistance <= currentDistance)
                    continue;

                var extendedPaths = paths[tableName]
                    .Select(path => path.Append(relationship).ToList())
                    .ToList();

                if (!distance.TryGetValue(nextTable, out _))
                {
                    distance[nextTable] = currentDistance + 1;
                    paths[nextTable] = extendedPaths;
                    queue.Enqueue(nextTable);
                }
                else
                {
                    paths[nextTable].AddRange(extendedPaths);
                }
            }
        }

        if (!paths.TryGetValue(targetTable, out var targetPaths))
            return [];

        return targetPaths
            .Select(path => (IReadOnlyList<DbRelationship>)path)
            .ToList();
    }

    private static IEnumerable<DbRelationship> GetAdjacentRelationships(
        IReadOnlyCollection<DbRelationship> relationships,
        string tableName)
    {
        return relationships
            .Where(r =>
                r.SourceTable.Equals(tableName, StringComparison.OrdinalIgnoreCase) ||
                r.TargetTable.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.SourceTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.SourceColumn, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.TargetTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.TargetColumn, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetNeighbor(DbRelationship relationship, string tableName) =>
        relationship.SourceTable.Equals(tableName, StringComparison.OrdinalIgnoreCase)
            ? relationship.TargetTable
            : relationship.SourceTable;

}
