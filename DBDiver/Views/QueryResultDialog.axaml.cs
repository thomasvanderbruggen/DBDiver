using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace DBDiver.Views;

public partial class QueryResultDialog : Window
{
    public QueryResultDialog(string sql)
    {
        InitializeComponent();
        SqlTextBox.Text = sql;
        CopyButton.Click += OnCopyClick;
        CloseButton.Click += (_, _) => Close();
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(SqlTextBox.Text);
    }
}
