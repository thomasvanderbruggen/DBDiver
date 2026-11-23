using System.Text.Json.Serialization;

namespace DBDiver.Models;

public class NaturalLanguageQueryDto
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("rejected")]
    public bool Rejected { get; init; }

    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; init; }

    [JsonPropertyName("select")]
    public NaturalLanguageSelectDto? Select { get; init; }

    [JsonPropertyName("from")]
    public List<string>? From { get; init; }

    [JsonPropertyName("where")]
    public List<NaturalLanguageWhereNodeDto> Where { get; init; } = [];

    [JsonPropertyName("groupBy")]
    public List<NaturalLanguageColumnReferenceDto> GroupBy { get; init; } = [];

    [JsonPropertyName("orderBy")]
    public List<NaturalLanguageOrderByDto> OrderBy { get; init; } = [];
}

public class NaturalLanguageSelectDto
{
    [JsonPropertyName("distinct")]
    public bool Distinct { get; init; }

    [JsonPropertyName("columns")]
    public required List<NaturalLanguageProjectionItemDto> Columns { get; init; }
}

public class NaturalLanguageProjectionItemDto
{
    [JsonPropertyName("table")]
    public required string Table { get; init; }

    [JsonPropertyName("column")]
    public required string Column { get; init; }

    [JsonPropertyName("aggregation")]
    public string? Aggregation { get; init; }

    [JsonPropertyName("alias")]
    public string? Alias { get; init; }
}

public class NaturalLanguageColumnReferenceDto
{
    [JsonPropertyName("table")]
    public required string Table { get; init; }

    [JsonPropertyName("column")]
    public required string Column { get; init; }
}

public class NaturalLanguageOrderByDto : NaturalLanguageColumnReferenceDto
{
    [JsonPropertyName("aggregation")]
    public string? Aggregation { get; init; }

    [JsonPropertyName("direction")]
    public string Direction { get; init; } = "ASC";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NaturalLanguageWhereConditionDto), "condition")]
[JsonDerivedType(typeof(NaturalLanguageWhereGroupDto), "group")]
public abstract class NaturalLanguageWhereNodeDto
{
    [JsonPropertyName("operator")]
    public string? Operator { get; init; }
}

public class NaturalLanguageWhereConditionDto : NaturalLanguageWhereNodeDto
{
    [JsonPropertyName("table")]
    public required string Table { get; init; }

    [JsonPropertyName("column")]
    public required string Column { get; init; }

    [JsonPropertyName("comparator")]
    public required string Comparator { get; init; }

    [JsonPropertyName("value")]
    public List<string> Values { get; init; } = [];
}

public class NaturalLanguageWhereGroupDto : NaturalLanguageWhereNodeDto
{
    [JsonPropertyName("children")]
    public required List<NaturalLanguageWhereNodeDto> Children { get; init; }
}
