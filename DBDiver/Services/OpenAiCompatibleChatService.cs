using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DBDiver.Models;

namespace DBDiver.Services;

public class OpenAiCompatibleChatService : IChatCompletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly ApiKeyCredentialService _credentialService;

    public OpenAiCompatibleChatService(HttpClient httpClient, ApiKeyCredentialService credentialService)
    {
        _httpClient = httpClient;
        _credentialService = credentialService;
    }

    public async Task<string> CompleteAsync(NaturalLanguageSettings settings, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var apiKey = (await _credentialService.GetApiKeyAsync())?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("No API key is configured.");

        var baseUrl = settings.BaseUrl.Trim();
        var modelName = settings.ModelName.Trim();
        if (string.IsNullOrWhiteSpace(modelName))
            throw new InvalidOperationException("No model name is configured.");

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"The configured Base URL '{baseUrl}' is not a valid HTTP or HTTPS URL.");

        var request = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CreateChatCompletionsUri(baseUrl));
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"The LLM request failed with status {response.StatusCode}: {responseJson}");

        var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponseDto>(responseJson, JsonOptions);
        return chatResponse?.Choices?.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("The LLM response did not contain a message.");
    }

    private class OpenAiChatResponseDto
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChatChoiceDto>? Choices { get; set; }
    }

    private class OpenAiChatChoiceDto
    {
        [JsonPropertyName("message")]
        public OpenAiChatMessageDto? Message { get; set; }
    }

    private class OpenAiChatMessageDto
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private static Uri CreateChatCompletionsUri(string baseUrl)
    {
        var versionedUrl = baseUrl.Trim().TrimEnd('/');
        if (!versionedUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            versionedUrl += "/v1";

        return new Uri($"{versionedUrl}/chat/completions");
    }
}
