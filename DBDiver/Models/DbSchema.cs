namespace DBDiver.Models;

public class DbSchema
{
    public List<DbTable> Tables { get; set; } = [];
    public List<DbRelationship> Relationships { get; set; } = [];
}
