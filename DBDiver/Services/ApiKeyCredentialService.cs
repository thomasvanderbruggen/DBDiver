using DBDiver.Models;
using Microsoft.Identity.Client.Extensions.Msal;

namespace DBDiver.Services;

public class ApiKeyCredentialService
{
    private const string ServiceName = "DBDiver";
    private const string AccountName = "OpenAICompatibleApiKey";
    private readonly string _cacheDirectory;

    public ApiKeyCredentialService()
    {
        _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DBDiver");
    }

    public Task<string?> GetApiKeyAsync()
    {
        var data = LoadCache();
        return Task.FromResult<string?>(data is { Length: > 0 } ? System.Text.Encoding.UTF8.GetString(data) : null);
    }

    public Task SetApiKeyAsync(string apiKey)
    {
        var helper = CreateHelper();
        helper.SaveUnencryptedTokenCache(System.Text.Encoding.UTF8.GetBytes(apiKey.Trim()));
        return Task.CompletedTask;
    }

    public Task ClearApiKeyAsync()
    {
        var helper = CreateHelper();
        helper.SaveUnencryptedTokenCache([]);
        return Task.CompletedTask;
    }

    private byte[]? LoadCache()
    {
        var helper = CreateHelper();
        return helper.LoadUnencryptedTokenCache();
    }

    private MsalCacheHelper CreateHelper()
    {
        Directory.CreateDirectory(_cacheDirectory);
        var properties = new StorageCreationPropertiesBuilder("dbdiver-api-key.cache", _cacheDirectory)
            .WithMacKeyChain(ServiceName, AccountName)
            .WithLinuxKeyring(
                "io.github.dbdiver.apikey",
                MsalCacheHelper.LinuxKeyRingDefaultCollection,
                "DB Diver OpenAI-compatible API key",
                new KeyValuePair<string, string>("ServiceName", ServiceName),
                new KeyValuePair<string, string>("AccountName", AccountName))
            .Build();

        return MsalCacheHelper.CreateAsync(properties).GetAwaiter().GetResult();
    }
}
