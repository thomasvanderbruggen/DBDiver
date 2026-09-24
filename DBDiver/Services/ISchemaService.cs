using DBDiver.Models;

namespace DBDiver.Services;

public interface ISchemaService
{
    DbSchema LoadSchema(string filePath);
    void SaveSchema(DbSchema schema, string filePath);
}
