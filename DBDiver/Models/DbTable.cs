namespace DBDiver.Models;

public class DbTable
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> PrimaryKeys { get; set; } = [];
    public List<DbColumn> Columns { get; set; } = [];

    public string QualifiedName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}
