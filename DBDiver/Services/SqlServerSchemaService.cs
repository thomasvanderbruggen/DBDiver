using System.Data.Common;
using DBDiver.Models;
using Serilog;

namespace DBDiver.Services;

public class SqlServerSchemaService
{
    private static readonly CsvSchemaService CsvSchemaService = new();

    public async Task<DbSchema> LoadSchemaAsync(
        DirectDatabaseConnection connection,
        CancellationToken cancellationToken = default)
    {
        var rows = new List<Dictionary<string, string>>();

        await using var dbConnection = SqlConnectionFactory.Create(connection);
        await dbConnection.OpenAsync(cancellationToken);

        await using var command = dbConnection.CreateCommand();
        command.CommandText = SqlServerSchemaQueries.Schema;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                row[reader.GetName(index)] = reader.IsDBNull(index)
                    ? string.Empty
                    : reader.GetValue(index).ToString() ?? string.Empty;

            rows.Add(row);
        }

        var schema = CsvSchemaService.LoadSchemaFromRows(rows);
        Log.Information(
            "Loaded SQL Server schema {Server}/{Database} with {TableCount} tables and {RelationshipCount} relationships",
            connection.Server,
            connection.Database,
            schema.Tables.Count,
            schema.Relationships.Count);

        return schema;
    }

    public async Task TestConnectionAsync(
        DirectDatabaseConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var dbConnection = SqlConnectionFactory.Create(connection);
        await dbConnection.OpenAsync(cancellationToken);
    }
}
