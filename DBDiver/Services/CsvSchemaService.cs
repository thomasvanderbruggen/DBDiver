using System.Globalization;
using System.Text;
using DBDiver.Models;
using Serilog;

namespace DBDiver.Services;

public class CsvSchemaService : ISchemaService
{
    private const string NullValue = "NULL";
    private static readonly string[] TypedSchemaRequiredColumns =
    [
        "schema", "table", "column_id", "column", "data_type", "is_nullable", "is_pk", "pk_ordinal"
    ];

    public DbSchema LoadSchema(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length == 0)
        {
            Log.Warning("CSV schema file {FilePath} is empty", filePath);
            return new DbSchema();
        }

        var header = ParseCsvLine(lines[0]);
        return IsTypedSchemaHeader(header) ? LoadTypedSchema(lines, filePath) : LoadLegacySchema(lines, filePath);
    }

    private static DbSchema LoadTypedSchema(string[] lines, string filePath)
    {
        var header = ParseCsvLine(lines[0]);
        var schema = new DbSchema();
        var tableIndex = new Dictionary<string, DbTable>(StringComparer.OrdinalIgnoreCase);
        var primaryKeys = new Dictionary<(string Schema, string Table), Dictionary<string, int>>();
        var relationshipOrdinals = new Dictionary<DbRelationship, int>();

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var row = ParseCsvRow(lines[i], header);
            var tableName = GetQualifiedTableName(
                row.GetValueOrDefault("schema", string.Empty),
                row.GetValueOrDefault("table", string.Empty));
            if (tableName is null)
            {
                Log.Information("Typed CSV schema row {Row} is a separator or null table row, skipping", i + 1);
                continue;
            }

            var tableIdentity = SplitQualifiedTableName(tableName);
            var qualifiedTableName = GetTableIdentifier(tableIdentity.Schema, tableIdentity.Table);
            if (!tableIndex.TryGetValue(tableName, out var table))
            {
                table = new DbTable { Schema = tableIdentity.Schema, Name = tableIdentity.Table };
                schema.Tables.Add(table);
                tableIndex[tableName] = table;
            }
            primaryKeys.TryAdd(tableIdentity, []);

            var columnName = row.GetValueOrDefault("column", string.Empty);
            if (string.IsNullOrEmpty(columnName) || columnName.Equals(NullValue, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!table.Columns.Any(column => column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
            {
                table.Columns.Add(new DbColumn
                {
                    Name = columnName,
                    DataType = row.GetValueOrDefault("data_type", string.Empty),
                    IsNullable = ParseBoolean(row.GetValueOrDefault("is_nullable", string.Empty), defaultValue: true)
                });
            }

            if (ParseBoolean(row.GetValueOrDefault("is_pk", string.Empty), defaultValue: false))
                primaryKeys[tableIdentity][columnName] = ParseInt32(row.GetValueOrDefault("pk_ordinal", string.Empty));

            var relationship = AddTypedRelationship(schema, tableIndex, table, row);
            if (relationship is not null)
                relationshipOrdinals[relationship] = ParseInt32(row.GetValueOrDefault("fk_ordinal", string.Empty));
        }

        foreach (var (tableIdentity, keys) in primaryKeys)
        {
            tableIndex[GetTableIdentifier(tableIdentity.Schema, tableIdentity.Table)].PrimaryKeys = keys
                .OrderBy(key => key.Value)
                .ThenBy(key => key.Key, StringComparer.OrdinalIgnoreCase)
                .Select(key => key.Key)
                .ToList();
        }

        foreach (var (relationship, ordinal) in relationshipOrdinals)
            relationship.ConstraintOrdinal = ordinal;

        Log.Information(
            "Loaded typed CSV schema {FilePath} with {TableCount} tables and {RelationshipCount} relationships",
            filePath, schema.Tables.Count, schema.Relationships.Count);
        return schema;
    }

    private static DbRelationship? AddTypedRelationship(
        DbSchema schema,
        Dictionary<string, DbTable> tableIndex,
        DbTable sourceTable,
        Dictionary<string, string> row)
    {
        var targetTable = GetQualifiedTableName(
            row.GetValueOrDefault("fk_target_schema", string.Empty),
            row.GetValueOrDefault("fk_target_table", string.Empty));
        if (targetTable is null)
            return null;

        if (!tableIndex.TryGetValue(targetTable, out var targetTableMetadata))
        {
            var targetIdentity = SplitQualifiedTableName(targetTable);
            targetTableMetadata = new DbTable { Schema = targetIdentity.Schema, Name = targetIdentity.Table };
            schema.Tables.Add(targetTableMetadata);
            tableIndex[targetTable] = targetTableMetadata;
        }

        var relationship = new DbRelationship
        {
            SourceTable = sourceTable.QualifiedName,
            SourceColumn = row.GetValueOrDefault("column", string.Empty),
            TargetTable = targetTable,
            TargetColumn = row.GetValueOrDefault("fk_target_column", string.Empty),
            ConstraintName = row.GetValueOrDefault("fk_name", string.Empty),
            IsCustom = ParseBoolean(row.GetValueOrDefault("is_custom", string.Empty), defaultValue: false)
        };

        var existingRelationship = schema.Relationships.FirstOrDefault(existing =>
            existing.SourceTable.Equals(relationship.SourceTable, StringComparison.OrdinalIgnoreCase) &&
            existing.SourceColumn.Equals(relationship.SourceColumn, StringComparison.OrdinalIgnoreCase) &&
            existing.TargetTable.Equals(relationship.TargetTable, StringComparison.OrdinalIgnoreCase) &&
            existing.TargetColumn.Equals(relationship.TargetColumn, StringComparison.OrdinalIgnoreCase));

        if (existingRelationship is not null)
            return existingRelationship;

        schema.Relationships.Add(relationship);
        return relationship;
    }

    private static DbSchema LoadLegacySchema(string[] lines, string filePath)
    {
        var header = ParseCsvLine(lines[0]);
        var schema = new DbSchema();
        var tableIndex = new Dictionary<string, DbTable>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var row = ParseCsvRow(lines[i], header);
            var tableName = row.GetValueOrDefault("table", string.Empty);
            if (!IsRealTableName(tableName))
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
            foreach (var pk in pkColumns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!table.PrimaryKeys.Contains(pk, StringComparer.OrdinalIgnoreCase))
                    table.PrimaryKeys.Add(pk);
                AddLegacyColumn(table, pk);
            }

            var fkSourceTable = row.GetValueOrDefault("fk_source_table", string.Empty);
            var fkSourceColumn = row.GetValueOrDefault("fk_source_column", string.Empty);
            var fkTargetTable = row.GetValueOrDefault("fk_target_table", string.Empty);
            var fkTargetColumn = row.GetValueOrDefault("fk_target_column", string.Empty);

            if (!IsRealTableName(fkSourceTable) || !IsRealTableName(fkTargetTable))
                continue;

            if (!tableIndex.TryGetValue(fkTargetTable, out var targetTable))
            {
                targetTable = new DbTable { Name = fkTargetTable };
                schema.Tables.Add(targetTable);
                tableIndex[fkTargetTable] = targetTable;
            }

            schema.Relationships.Add(new DbRelationship
            {
                SourceTable = fkSourceTable,
                SourceColumn = fkSourceColumn,
                TargetTable = fkTargetTable,
                TargetColumn = fkTargetColumn,
                IsCustom = ParseBoolean(row.GetValueOrDefault("is_custom", string.Empty), defaultValue: false)
            });

            AddLegacyColumn(table, fkSourceColumn);
            AddLegacyColumn(targetTable, fkTargetColumn);
        }

        Log.Information(
            "Loaded legacy CSV schema {FilePath} with {TableCount} tables and {RelationshipCount} relationships",
            filePath, schema.Tables.Count, schema.Relationships.Count);
        return schema;
    }

    private static void AddLegacyColumn(DbTable table, string columnName)
    {
        if (string.IsNullOrEmpty(columnName) ||
            columnName.Equals(NullValue, StringComparison.OrdinalIgnoreCase) ||
            table.Columns.Any(column => column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
            return;

        table.Columns.Add(new DbColumn { Name = columnName });
    }

    public void SaveSchema(DbSchema schema, string filePath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("schema,table,column_id,column,data_type,is_nullable,is_pk,pk_ordinal,fk_name,fk_ordinal,fk_source_schema,fk_source_table,fk_source_column,fk_target_schema,fk_target_table,fk_target_column,is_custom");

        foreach (var table in schema.Tables)
        {
            var tableParts = (table.Schema, table.Name);
            var relationships = schema.Relationships
                .Where(relationship => relationship.SourceTable.Equals(table.QualifiedName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var (column, columnIndex) in table.Columns.Select((value, index) => (value, index)))
            {
                var pkOrdinal = table.PrimaryKeys
                    .FindIndex(pk => pk.Equals(column.Name, StringComparison.OrdinalIgnoreCase)) + 1;
                var columnRelationships = relationships
                    .Where(relationship => relationship.SourceColumn.Equals(column.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (columnRelationships.Count == 0)
                    AppendTypedSchemaRow(builder, tableParts, columnIndex, column, pkOrdinal, null, 0);
                else
                    for (var relationshipOrdinal = 0; relationshipOrdinal < columnRelationships.Count; relationshipOrdinal++)
                        AppendTypedSchemaRow(
                            builder,
                            tableParts,
                            columnIndex,
                            column,
                            pkOrdinal,
                            columnRelationships[relationshipOrdinal],
                            columnRelationships[relationshipOrdinal].ConstraintOrdinal > 0
                                ? columnRelationships[relationshipOrdinal].ConstraintOrdinal
                                : relationshipOrdinal + 1);
            }

            foreach (var (relationship, index) in relationships
                .Where(relationship => !table.Columns.Any(column => column.Name.Equals(relationship.SourceColumn, StringComparison.OrdinalIgnoreCase)))
                .Select((value, index) => (value, index)))
                AppendTypedSchemaRow(builder, tableParts, -1, null, 0, relationship, index + 1);
        }

        File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        Log.Information(
            "Saved typed CSV schema {FilePath} with {TableCount} tables and {RelationshipCount} relationships",
            filePath, schema.Tables.Count, schema.Relationships.Count);
    }

    private static void AppendTypedSchemaRow(
        StringBuilder builder,
        (string Schema, string Table) tableParts,
        int columnIndex,
        DbColumn? column,
        int pkOrdinal,
        DbRelationship? relationship,
        int fkOrdinal)
    {
        var columnName = column?.Name ?? relationship?.SourceColumn ?? string.Empty;
        var isPk = pkOrdinal > 0;
        var targetParts = relationship is null
            ? (Schema: string.Empty, Table: string.Empty)
            : SplitQualifiedTableName(relationship.TargetTable);

        builder.Append(EscapeCsvValue(tableParts.Schema)).Append(',')
            .Append(EscapeCsvValue(tableParts.Table)).Append(',')
            .Append(columnIndex >= 0 ? columnIndex.ToString(CultureInfo.InvariantCulture) : NullValue).Append(',')
            .Append(EscapeCsvValue(columnName)).Append(',')
            .Append(EscapeCsvValue(column?.DataType ?? string.Empty)).Append(',')
            .Append((column?.IsNullable ?? true) ? "true" : "false").Append(',')
            .Append(isPk ? "true" : "false").Append(',')
            .Append(isPk ? pkOrdinal.ToString(CultureInfo.InvariantCulture) : NullValue).Append(',')
            .Append(EscapeCsvValue(relationship?.ConstraintName ?? string.Empty)).Append(',')
            .Append(relationship is null ? NullValue : fkOrdinal.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(EscapeCsvValue(tableParts.Schema)).Append(',')
            .Append(EscapeCsvValue(tableParts.Table)).Append(',')
            .Append(EscapeCsvValue(relationship?.SourceColumn ?? string.Empty)).Append(',')
            .Append(EscapeCsvValue(targetParts.Schema)).Append(',')
            .Append(EscapeCsvValue(targetParts.Table)).Append(',')
            .Append(EscapeCsvValue(relationship?.TargetColumn ?? string.Empty)).Append(',')
            .Append((relationship?.IsCustom ?? false) ? "true" : "false");
        builder.AppendLine();
    }

    private static (string Schema, string Table) SplitQualifiedTableName(string qualifiedName)
    {
        var separatorIndex = qualifiedName.IndexOf('.');
        return separatorIndex <= 0
            ? (string.Empty, qualifiedName)
            : (qualifiedName[..separatorIndex], qualifiedName[(separatorIndex + 1)..]);
    }

    private static string GetTableIdentifier(string schemaName, string tableName) =>
        string.IsNullOrEmpty(schemaName) ? tableName : $"{schemaName}.{tableName}";

    private static string? GetQualifiedTableName(string schemaName, string tableName)
    {
        if (string.IsNullOrEmpty(tableName) ||
            tableName.StartsWith('-') ||
            tableName.Equals(NullValue, StringComparison.OrdinalIgnoreCase))
            return null;

        return string.IsNullOrEmpty(schemaName) ||
            schemaName.StartsWith('-') ||
            schemaName.Equals(NullValue, StringComparison.OrdinalIgnoreCase)
            ? tableName
            : $"{schemaName}.{tableName}";
    }

    private static Dictionary<string, string> ParseCsvRow(string line, string[] header)
    {
        var fields = ParseCsvLine(line);
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length && i < fields.Length; i++)
            row[header[i]] = fields[i].Trim();
        return row;
    }

    private static bool ParseBoolean(string value, bool defaultValue) => value switch
    {
        "1" => true,
        "0" => false,
        _ when value.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) => true,
        _ when value.Equals(bool.FalseString, StringComparison.OrdinalIgnoreCase) => false,
        _ => defaultValue
    };

    private static int ParseInt32(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static bool IsTypedSchemaHeader(string[] header) =>
        TypedSchemaRequiredColumns.All(required => header.Contains(required, StringComparer.OrdinalIgnoreCase));

    private static bool IsRealTableName(string tableName) =>
        !string.IsNullOrEmpty(tableName) &&
        !tableName.StartsWith('-') &&
        !tableName.Equals(NullValue, StringComparison.OrdinalIgnoreCase);

    private static string EscapeCsvValue(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var escapedQuote = false;
        var current = new StringBuilder();

        foreach (var character in line)
        {
            if (character == '"')
            {
                if (!inQuotes && current.Length == 0)
                    inQuotes = true;
                else if (escapedQuote)
                {
                    current.Append('"');
                    escapedQuote = false;
                }
                else if (inQuotes)
                    escapedQuote = true;
                else
                    current.Append('"');
            }
            else if (character == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                escapedQuote = false;
            }
            else
            {
                current.Append(character);
                escapedQuote = false;
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
