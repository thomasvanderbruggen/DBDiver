using Avalonia.Controls;
using DBDiver.Models;
using DBDiver.ViewModels;

namespace DBDiver.Views;

public partial class DirectDatabaseConnectionWindow : Window
{
    private readonly DirectDatabaseConnectionViewModel _viewModel;

    public DirectDatabaseConnectionWindow(DirectDatabaseConnectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.SchemaLoaded += OnSchemaLoaded;
        Closing += (_, _) => viewModel.ClearPassword();
        Closed += (_, _) => viewModel.ClearPassword();
    }

    private void OnSchemaLoaded(DbSchema schema)
    {
        Close(schema);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
