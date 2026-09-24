namespace DBDiver.Models;

public class DbTable
{
    public string Name { get; set; } = string.Empty;
    public List<string> PrimaryKeys { get; set; } = [];
    public List<string> Columns { get; set; } = [];
}
