using DBDiver.Models;

namespace DBDiver.Services;

public interface IChatCompletionService
{
    Task<string> CompleteAsync(
        NaturalLanguageSettings settings,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken);
}
