using System.Text.Json;
using DBDiver.Models;
using DBDiver.Services;
using Xunit;

namespace DBDiver.Tests;

public class NaturalLanguageQueryOrchestratorTests
{
    private const string ValidResponse = """
        {
          "version": 1,
          "select": {
            "distinct": false,
            "columns": [
              { "table": "Products", "column": "Name" }
            ]
          },
          "from": ["Products"]
        }
        """;

    private const string InvalidResponse = "{not-json";

    [Fact]
    public async Task GenerateAsync_SucceedsWithoutRetryForValidResponse()
    {
        var chatService = new FakeChatCompletionService([ValidResponse]);
        var result = await CreateOrchestrator(chatService).GenerateAsync(CreateSchema(), "Show products", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidResponse, result.RawResponse);
        Assert.Single(chatService.Prompts);
    }

    [Fact]
    public async Task GenerateAsync_RetriesMalformedJsonOnce()
    {
        var chatService = new FakeChatCompletionService([InvalidResponse, ValidResponse]);
        var result = await CreateOrchestrator(chatService).GenerateAsync(CreateSchema(), "Show products", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, chatService.Prompts.Count);
        Assert.Contains("The response was not valid JSON", chatService.Prompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_FailsAfterOneRetry()
    {
        var chatService = new FakeChatCompletionService([InvalidResponse, InvalidResponse]);
        var result = await CreateOrchestrator(chatService).GenerateAsync(CreateSchema(), "Show products", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.QueryModel);
        Assert.Equal(InvalidResponse, result.RawResponse);
        Assert.Equal(2, chatService.Prompts.Count);
        Assert.Contains(result.Errors, error => error.Contains("The response was not valid JSON", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerateAsync_DoesNotRetryModelRejection()
    {
        var rejectionResponse = JsonSerializer.Serialize(new NaturalLanguageQueryDto
        {
            Rejected = true,
            RejectionReason = "Only SELECT queries are supported."
        });
        var chatService = new FakeChatCompletionService([rejectionResponse]);
        var result = await CreateOrchestrator(chatService).GenerateAsync(CreateSchema(), "Delete products", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.QueryModel);
        Assert.Single(chatService.Prompts);
        Assert.Contains(result.Errors, error => error.Contains("Only SELECT queries are supported.", StringComparison.Ordinal));
    }

    private static NaturalLanguageQueryOrchestrator CreateOrchestrator(IChatCompletionService chatService) =>
        new(chatService, "Products(ProductID int PK, Name nvarchar)", new NaturalLanguageSettings());

    private static DbSchema CreateSchema() => new()
    {
        Tables =
        [
            new DbTable
            {
                Name = "Products",
                Columns =
                [
                    new DbColumn { Name = "ProductID", DataType = "int" },
                    new DbColumn { Name = "Name", DataType = "nvarchar" }
                ]
            }
        ]
    };

    private sealed class FakeChatCompletionService(IReadOnlyList<string> responses) : IChatCompletionService
    {
        private int _index;

        public List<string> Prompts { get; } = [];

        public Task<string> CompleteAsync(
            NaturalLanguageSettings settings,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            Prompts.Add(userPrompt);
            return Task.FromResult(responses[Math.Min(_index++, responses.Count - 1)]);
        }
    }
}
