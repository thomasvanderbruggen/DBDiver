using System.ComponentModel;
using System.Windows.Input;
using DBDiver.Models;
using DBDiver.Services;

namespace DBDiver.ViewModels;

public class DirectDatabaseConnectionViewModel : ViewModelBase
{
    private readonly SqlServerSchemaService _schemaService = new();
    private readonly DirectDatabaseConnection _connection = new();
    private string _statusText = string.Empty;
    private bool _isBusy;

    public DirectDatabaseConnectionViewModel()
    {
        TestConnectionCommand = new RelayCommand(_ => _ = TestConnectionAsync(), _ => !IsBusy);
        ConnectCommand = new RelayCommand(_ => _ = ConnectAsync(), _ => !IsBusy && _connection.IsValid);
    }

    public event Action<DbSchema>? SchemaLoaded;

    public string Server
    {
        get => _connection.Server;
        set
        {
            if (_connection.Server == value)
                return;

            _connection.Server = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public string Database
    {
        get => _connection.Database;
        set
        {
            if (_connection.Database == value)
                return;

            _connection.Database = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public string Username
    {
        get => _connection.Username;
        set
        {
            if (_connection.Username == value)
                return;

            _connection.Username = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public string Password
    {
        get => _connection.Password;
        set
        {
            if (_connection.Password == value)
                return;

            _connection.Password = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public bool UseWindowsAuthentication
    {
        get => _connection.UseWindowsAuthentication;
        set
        {
            if (_connection.UseWindowsAuthentication == value)
                return;

            _connection.UseWindowsAuthentication = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSqlAuthentication));
            RaiseCanExecuteChanged();
        }
    }

    public bool IsSqlAuthentication => !UseWindowsAuthentication;

    public bool TrustServerCertificate
    {
        get => _connection.TrustServerCertificate;
        set
        {
            if (_connection.TrustServerCertificate == value)
                return;

            _connection.TrustServerCertificate = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
                return;

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public ICommand TestConnectionCommand { get; }
    public ICommand ConnectCommand { get; }

    public void ClearPassword() => _connection.Password = string.Empty;

    private async Task TestConnectionAsync()
    {
        await RunDatabaseOperationAsync("Connection test failed.", async () =>
        {
            await _schemaService.TestConnectionAsync(_connection);
            StatusText = "Connection test succeeded.";
            return true;
        });
    }

    private async Task ConnectAsync()
    {
        var schema = await RunDatabaseOperationAsync("Could not load schema.", async () =>
        {
            var loadedSchema = await _schemaService.LoadSchemaAsync(_connection);
            StatusText = $"Loaded {loadedSchema.Tables.Count} tables and {loadedSchema.Relationships.Count} relationships.";
            return loadedSchema;
        });

        if (schema is not null)
            SchemaLoaded?.Invoke(schema);
    }

    private async Task<T?> RunDatabaseOperationAsync<T>(string failureText, Func<Task<T>> operation)
    {
        if (IsBusy)
            return default;

        IsBusy = true;
        StatusText = "Working...";

        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            StatusText = $"{failureText} {ex.Message}";
            return default;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCanExecuteChanged()
    {
        ((RelayCommand)TestConnectionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
    }
}
