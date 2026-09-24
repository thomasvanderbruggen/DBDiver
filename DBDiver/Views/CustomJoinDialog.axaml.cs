using Avalonia.Controls;
using DBDiver.Models;

namespace DBDiver.Views;

public partial class CustomJoinDialog : Window
{
    private readonly DbTable _sourceTable;
    private readonly DbTable _targetTable;

    public string SourceColumn { get; private set; } = string.Empty;
    public string TargetColumn { get; private set; } = string.Empty;

    public CustomJoinDialog(DbTable sourceTable, DbTable targetTable)
    {
        InitializeComponent();
        _sourceTable = sourceTable;
        _targetTable = targetTable;

        Title = $"Custom Join: {sourceTable.Name} -> {targetTable.Name}";
        PreviewText.Text = $"{sourceTable.Name}.? -> {targetTable.Name}.?";
        SourceColumnBox.ItemsSource = GetColumnSuggestions(sourceTable, targetTable);
        TargetColumnBox.ItemsSource = GetColumnSuggestions(targetTable, sourceTable);

        SourceColumnBox.TextChanged += (_, _) =>
        {
            UpdatePreview();
            UpdateConfirmState();
        };
        TargetColumnBox.TextChanged += (_, _) =>
        {
            UpdatePreview();
            UpdateConfirmState();
        };

        CancelButton.Click += (_, _) => CloseAsCanceled();
        ConfirmButton.Click += (_, _) => Confirm();
        UpdatePreview();
        UpdateConfirmState();
    }

    private void UpdatePreview()
    {
        var source = string.IsNullOrWhiteSpace(SourceColumnBox.Text) ? "?" : SourceColumnBox.Text.Trim();
        var target = string.IsNullOrWhiteSpace(TargetColumnBox.Text) ? "?" : TargetColumnBox.Text.Trim();
        PreviewText.Text = $"{_sourceTable.Name}.{source} -> {_targetTable.Name}.{target}";
    }

    private void UpdateConfirmState()
    {
        ConfirmButton.IsEnabled =
            !string.IsNullOrWhiteSpace(SourceColumnBox.Text) &&
            !string.IsNullOrWhiteSpace(TargetColumnBox.Text);
    }

    private static IReadOnlyList<string> GetColumnSuggestions(DbTable table, DbTable otherTable)
    {
        var knownColumns = table.Columns
            .Concat(table.PrimaryKeys)
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchingColumns = otherTable.Columns
            .Concat(otherTable.PrimaryKeys)
            .Where(knownColumns.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return matchingColumns
            .Concat(knownColumns.Except(matchingColumns, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private void Confirm()
    {
        SourceColumn = SourceColumnBox.Text?.Trim() ?? string.Empty;
        TargetColumn = TargetColumnBox.Text?.Trim() ?? string.Empty;
        base.Close(true);
    }

    private void CloseAsCanceled()
    {
        SourceColumn = string.Empty;
        TargetColumn = string.Empty;
        base.Close();
    }
}
