using DBDiver.Models;
using DBDiver.Services;

namespace DBDiver.ViewModels;

public class NaturalLanguageQueryViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;
    private readonly ApiKeyCredentialService _credentialService = new();
    private readonly OpenAiCompatibleChatService _chatService;
    private readonly CompactSchemaDdlService _ddlService = new();
    private string _request = string.Empty;
    private bool _isBusy;
    private string _lastError = string.Empty;
    private DbSchema? _schema;
    private string _compactDdl = string.Empty;

    public RelayCommand GenerateCommand { get; }

    public NaturalLanguageQueryViewModel(AppSettings appSettings)
    {
        _appSettings = appSettings;
        _chatService = new OpenAiCompatibleChatService(new HttpClient(), _credentialService);
        GenerateCommand = new RelayCommand(_ => Generate());
    }

    public event Action<QueryModel, IReadOnlyList<DbRelationship>>? QueryGenerated;

    public string Request
    {
        get => _request;
        set
        {
            _request = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public DbSchema? Schema
    {
        get => _schema;
        set
        {
            _schema = value;
            _compactDdl = value is null ? string.Empty : _ddlService.Build(value);
            OnPropertyChanged();
        }
    }

    public string LastError
    {
        get => _lastError;
        private set
        {
            _lastError = value;
            OnPropertyChanged();
        }
    }

    private async Task GenerateAsync(DbSchema schema)
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(Request))
        {
            LastError = "Enter a request first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_appSettings.NaturalLanguage.ModelName))
        {
            LastError = "Configure a model name in Settings.";
            return;
        }

        if (await _credentialService.GetApiKeyAsync() is null)
        {
            LastError = "Configure an API key in Settings.";
            return;
        }

        IsBusy = true;
        LastError = string.Empty;

        try
        {
            var orchestrator = new NaturalLanguageQueryOrchestrator(
                _chatService,
                _compactDdl,
                _appSettings.NaturalLanguage);
            var result = await orchestrator.GenerateAsync(schema, Request, CancellationToken.None);

            if (result.IsSuccess)
            {
                QueryGenerated?.Invoke(result.QueryModel!, result.JoinRelationships);
                return;
            }

            LastError = result.Errors.Count > 0
                ? string.Join("\n", result.Errors)
                : "The model response could not be converted into a valid query.";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async void Generate()
    {
        if (Schema is { } schema)
            await GenerateAsync(schema);
    }
}
