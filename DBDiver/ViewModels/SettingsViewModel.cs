using System.Collections.ObjectModel;
using Avalonia.Media;
using DBDiver.Models;
using DBDiver.Services;

namespace DBDiver.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private bool _isDirty;

    public event Action<AppSettings>? SettingsChanged;

    public SettingsViewModel(SettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
    }

    public GraphThemeSettings Theme => _settings.Theme;
    public GraphLayoutSettings Layout => _settings.Layout;
    public QueryBuilderSettings QueryBuilder => _settings.QueryBuilder;

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            _isDirty = value;
            OnPropertyChanged();
        }
    }

    public void MarkDirty()
    {
        IsDirty = true;
        SettingsChanged?.Invoke(_settings);
    }

    public void Save()
    {
        _settingsService.Save(_settings);
        IsDirty = false;
    }

    public void ResetTheme()
    {
        _settings.Theme = new GraphThemeSettings();
        OnPropertyChanged(nameof(Theme));
        MarkDirty();
    }

    public void ResetLayout()
    {
        _settings.Layout = new GraphLayoutSettings();
        OnPropertyChanged(nameof(Layout));
        MarkDirty();
    }

    public void ResetQueryBuilder()
    {
        _settings.QueryBuilder = new QueryBuilderSettings();
        OnPropertyChanged(nameof(QueryBuilder));
        MarkDirty();
    }

    public void RaiseThemeChanged()
    {
        OnPropertyChanged(nameof(Theme));
        MarkDirty();
    }

    public void RaiseLayoutChanged()
    {
        OnPropertyChanged(nameof(Layout));
        MarkDirty();
    }

    public void RaiseQueryBuilderChanged()
    {
        OnPropertyChanged(nameof(QueryBuilder));
        MarkDirty();
    }
}
