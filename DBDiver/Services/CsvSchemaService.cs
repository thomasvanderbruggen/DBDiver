using System.Globalization;
using System.Text;
using DBDiver.Models;
using Serilog;

namespace DBDiver.Services;

public class CsvSchemaService : ISchemaService
{
    private const string NullValue = "NULL";

    public DbSchema LoadSchema(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length == 0)
        {
            Log.Warning("CSV schema file {FilePath} is empty", filePath);
            return new DbSchema();
        }

        var header = ParseCsvLine(lines[0]);
        var schema = new DbSchema();
        var tableIndex = new Dictionary<string, DbTable>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var fields = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < header.Length && j < fields.Length; j++)
                row[header[j]] = fields[j].Trim();

            var tableName = row.GetValueOrDefault("table", string.Empty);
            if (string.IsNullOrEmpty(tableName) || tableName.StartsWith('-') || tableName.Equals(NullValue, StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("CSV row {Row} is a separator or null table row, skipping", i + 1);
                continue;
            }

            if (!tableIndex.TryGetValue(tableName, out var table))
            {
                table = new DbTable { Name = tableName };
                schema.Tables.Add(table);
                tableIndex[tableName] = table;
            }

            var pkColumns = row.GetValueOrDefault("pk_columns", string.Empty);
            if (!string.IsNullOrEmpty(pkColumns))
            {
                foreach (var pk in pkColumns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!table.PrimaryKeys.Contains(pk, StringComparer.OrdinalIgnoreCase))
                        table.PrimaryKeys.Add(pk);
                    if (!table.Columns.Contains(pk, StringComparer.OrdinalIgnoreCase))
                        table.Columns.Add(pk);
                }
            }

            var fkSourceTable = row.GetValueOrDefault("fk_source_table", string.Empty);
            var fkSourceColumn = row.GetValueOrDefault("fk_source_column", string.Empty);
            var fkTargetTable = row.GetValueOrDefault("fk_target_table", string.Empty);
            var fkTargetColumn = row.GetValueOrDefault("fk_target_column", string.Empty);

            if (IsRealTableName(fkSourceTable) && IsRealTableName(fkTargetTable))
            {
                if (!tableIndex.TryGetValue(fkTargetTable, out var targetTable))
                {
                    targetTable = new DbTable { Name = fkTargetTable };
                    schema.Tables.Add(targetTable);
                    tableIndex[fkTargetTable] = targetTable;
                }

                var relationship = new DbRelationship
                {
                    SourceTable = fkSourceTable,
                    SourceColumn = fkSourceColumn,
                    TargetTable = fkTargetTable,
                    TargetColumn = fkTargetColumn,
                    IsCustom = row.GetValueOrDefault("is_custom", string.Empty).Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase)
                };
                schema.Relationships.Add(relationship);

                if (!string.IsNullOrEmpty(fkSourceColumn) && !fkSourceColumn.Equals(NullValue, StringComparison.OrdinalIgnoreCase) &&
                    !table.Columns.Contains(fkSourceColumn, StringComparer.OrdinalIgnoreCase))
                    table.Columns.Add(fkSourceColumn);
                if (!string.IsNullOrEmpty(fkTargetColumn) && !fkTargetColumn.Equals(NullValue, StringComparison.OrdinalIgnoreCase) &&
                    !targetTable.Columns.Contains(fkTargetColumn, StringComparer.OrdinalIgnoreCase))
                    targetTable.Columns.Add(fkTargetColumn);
            }
        }

        Log.Information("Loaded CSV schema {FilePath} with {TableCount} tables and {RelationshipCount} relationships",
            filePath, schema.Tables.Count, schema.Relationships.Count);
        return schema;
    }

    private static bool IsRealTableName(string tableName) =>
        !string.IsNullOrEmpty(tableName) &&
        !tableName.StartsWith('-') &&
        !tableName.Equals(NullValue, StringComparison.OrdinalIgnoreCase);

    public void SaveSchema(DbSchema schema, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("table,pk_columns,fk_source_table,fk_source_column,fk_target_table,fk_target_column,is_custom");

        foreach (var table in schema.Tables)
        {
            var pk = string.Join(";", table.PrimaryKeys);
            var relationships = schema.Relationships
                .Where(r => r.SourceTable.Equals(table.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (relationships.Count == 0)
            {
                sb.AppendLine($"{table.Name},{pk},,,,,");
            }
            else
            {
                foreach (var rel in relationships)
                {
                    sb.AppendLine($"{table.Name},{pk},{rel.SourceTable},{rel.SourceColumn},{rel.TargetTable},{rel.TargetColumn},{rel.IsCustom}");
                }
            }
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        Log.Information("Saved CSV schema {FilePath} with {TableCount} tables and {RelationshipCount} relationships",
            filePath, schema.Tables.Count, schema.Relationships.Count);
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
