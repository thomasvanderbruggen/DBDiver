namespace DBDiver.Models;

public class DbRelationship
{
    public string SourceTable { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string TargetColumn { get; set; } = string.Empty;
    public string ConstraintName { get; set; } = string.Empty;
    public int ConstraintOrdinal { get; set; }
    public bool IsCustom { get; set; }
}
