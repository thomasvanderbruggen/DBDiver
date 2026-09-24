using System.IO;
using System.Text.Json;
using DBDiver.Models;
using Serilog;

namespace DBDiver.Services;

public class SchemaService : ISchemaService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public DbSchema LoadSchema(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var schema = JsonSerializer.Deserialize<DbSchema>(json, JsonOptions) ?? new DbSchema();
        Log.Information("Loaded schema {FilePath} with {TableCount} tables and {RelationshipCount} relationships",
            filePath, schema.Tables.Count, schema.Relationships.Count);
        return schema;
    }

    public void SaveSchema(DbSchema schema, string filePath)
    {
        var json = JsonSerializer.Serialize(schema, JsonOptions);
        File.WriteAllText(filePath, json);
        Log.Information("Saved schema {FilePath} with {TableCount} tables and {RelationshipCount} relationships",
            filePath, schema.Tables.Count, schema.Relationships.Count);
    }
}
