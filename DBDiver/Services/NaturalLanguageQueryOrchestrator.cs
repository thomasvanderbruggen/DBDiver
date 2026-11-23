using System.Text.Json;
using DBDiver.Models;

namespace DBDiver.Services;

public class NaturalLanguageQueryOrchestrator
{
    private readonly IChatCompletionService _chatService;
    private readonly string _compactDdl;
    private readonly NaturalLanguageSettings _settings;

    public NaturalLanguageQueryOrchestrator(
        IChatCompletionService chatService,
        string compactDdl,
        NaturalLanguageSettings settings)
    {
        _chatService = chatService;
        _compactDdl = compactDdl;
        _settings = settings;
    }

    public async Task<NaturalLanguageQueryResult> GenerateAsync(DbSchema schema, string request, CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(request, _compactDdl);
        var rawResponse = await _chatService.CompleteAsync(_settings, systemPrompt, userPrompt, cancellationToken);
        var validationResult = ValidateResponse(schema, rawResponse, out var dto);

        if (dto?.Rejected == true)
            return NaturalLanguageQueryResult.Failure(validationResult.Errors, rawResponse);

        if (validationResult.Errors.Count > 0)
        {
            var retryPrompt = BuildRetryPrompt(userPrompt, validationResult.Errors);
            rawResponse = await _chatService.CompleteAsync(
                _settings,
                systemPrompt,
                retryPrompt,
                cancellationToken);
            validationResult = ValidateResponse(schema, rawResponse, out dto);
        }

        if (validationResult.Errors.Count > 0 || validationResult.QueryModel is null || dto is null)
        {
            return NaturalLanguageQueryResult.Failure(
                validationResult.Errors,
                rawResponse);
        }

        return NaturalLanguageQueryResult.Success(
            validationResult.QueryModel,
            validationResult.JoinRelationships,
            rawResponse);
    }

    private static NaturalLanguageQueryValidationResult ValidateResponse(
        DbSchema schema,
        string rawResponse,
        out NaturalLanguageQueryDto? dto)
    {
        dto = null;

        try
        {
            var json = ExtractJson(rawResponse);
            var queryPathService = new QueryPathService();
            var queryService = new NaturalLanguageQueryService(schema, queryPathService);
            dto = queryService.Deserialize(json);
            return queryService.Validate(dto);
        }
        catch (JsonException exception)
        {
            var position = exception.LineNumber >= 0 ? $" at line {exception.LineNumber + 1}" : string.Empty;
            var bytePosition = exception.BytePositionInLine >= 0 ? $", byte {exception.BytePositionInLine}" : string.Empty;
            var result = NaturalLanguageQueryValidationResult.Invalid(
                $"The response was not valid JSON{position}{bytePosition}: {exception.Message}");
            return result;
        }
    }

    private static string ExtractJson(string response)
    {
        var trimmed = response.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
                trimmed = trimmed[(firstNewLine + 1)..];

            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
                trimmed = trimmed[..closingFence];
        }

        return trimmed.Trim();
    }

    private static string BuildSystemPrompt() =>
        """
        You translate natural-language database questions into strict JSON for a SQL SELECT query.
        Return only valid JSON. Do not include markdown or explanations.
        Use SQL Server-compatible identifiers exactly as provided in the compact schema DDL.
        The FROM array must contain every table used by SELECT, WHERE, GROUP BY, and ORDER BY.
        DB Diver infers join paths and adds intermediate tables, so FROM only needs tables that supply referenced columns.
        If the request cannot be answered by a SQL SELECT query, set rejected to true and explain rejectionReason.
        Do not translate INSERT, UPDATE, DELETE, MERGE, DDL, or other statement types into a query.
        Comparators are written as SQL operators. Values must be arrays.
        The first WHERE node must omit operator; subsequent nodes use AND or OR.
        """;

    private static string BuildUserPrompt(string request, string ddl) =>
        $$"""
        Request: {{request}}

        Compact schema DDL:
        {{ddl}}

        Return JSON with this structure:
        {
          "version": 1,
          "rejected": false,
          "rejectionReason": null,
          "select": {
            "distinct": false,
            "columns": [
              { "table": "TableName", "column": "ColumnName", "aggregation": "COUNT|SUM|AVG|MIN|MAX", "alias": "AliasName" }
            ]
          },
          "from": ["TableName"],
          "where": [
            {
              "type": "condition",
              "table": "TableName",
              "column": "ColumnName",
              "comparator": "=",
              "value": ["LiteralValue","IncludeSubsequentValuesForComparatorsThatUseMoreThanOne"],
              "operator": "AND|OR"
            },
            {
              "type": "group",
              "children": [],
              "operator": "AND|OR"
            }
          ],
          "groupBy": [{ "table": "TableName", "column": "ColumnName" }],
          "orderBy": [{ "table": "TableName", "column": "ColumnName", "aggregation": "COUNT|SUM|AVG|MIN|MAX", "direction": "ASC|DESC" }]
        }
        """;

    private static string BuildRetryPrompt(string originalPrompt, IReadOnlyList<string> errors) =>
        $$"""
        Your previous response was invalid.

        Validation errors:
        {{string.Join("\n", errors)}}

        Please return only a corrected JSON object matching the requested structure.

        {{originalPrompt}}
        """;
}

public class NaturalLanguageQueryResult
{
    private NaturalLanguageQueryResult(
        QueryModel? queryModel,
        IReadOnlyList<DbRelationship> joinRelationships,
        IReadOnlyList<string> errors,
        string rawResponse)
    {
        QueryModel = queryModel;
        JoinRelationships = joinRelationships;
        Errors = errors;
        RawResponse = rawResponse;
    }

    public QueryModel? QueryModel { get; }
    public IReadOnlyList<DbRelationship> JoinRelationships { get; }
    public IReadOnlyList<string> Errors { get; }
    public string RawResponse { get; }
    public bool IsSuccess => QueryModel is not null && Errors.Count == 0;

    public static NaturalLanguageQueryResult Success(
        QueryModel queryModel,
        IReadOnlyList<DbRelationship> joinRelationships,
        string rawResponse) =>
        new(queryModel, joinRelationships, [], rawResponse);

    public static NaturalLanguageQueryResult Failure(IReadOnlyList<string> errors, string rawResponse) =>
        new(null, [], errors, rawResponse);
}
