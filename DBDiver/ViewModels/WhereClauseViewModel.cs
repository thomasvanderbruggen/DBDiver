using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using DBDiver.Models;

namespace DBDiver.ViewModels;

public class WhereColumnOption
{
    public WhereColumnOption(QueryTable table, DbColumn column)
    {
        Table = table;
        Column = column;
        DisplayName = column.Name;
        QualifiedDisplayName = $"{table.Name}.{column.Name}";
    }

    public QueryTable Table { get; }
    public DbColumn Column { get; }
    public string DisplayName { get; }
    public string QualifiedDisplayName { get; } = string.Empty;
}

public class WhereDropdownOption
{
    public WhereDropdownOption(string tableDisplayName)
    {
        DisplayName = tableDisplayName;
        IsHeader = true;
        QualifiedDisplayName = tableDisplayName;
    }

    public WhereDropdownOption(WhereColumnOption column)
    {
        Column = column;
        DisplayName = column.DisplayName;
        QualifiedDisplayName = column.QualifiedDisplayName;
    }

    public WhereColumnOption? Column { get; }
    public string DisplayName { get; }
    public bool IsHeader { get; }
    public string QualifiedDisplayName { get; }
    public string SelectedDisplayName => Column?.QualifiedDisplayName ?? DisplayName;
}

public class WhereOperatorOption
{
    public WhereOperatorOption(WhereClauseOperator @operator, string displayName, bool usesBetween)
    {
        Operator = @operator;
        DisplayName = displayName;
        UsesBetween = usesBetween;
    }

    public WhereClauseOperator Operator { get; }
    public string DisplayName { get; }
    public bool UsesBetween { get; }
}

public class WhereConnectorOption
{
    public WhereConnectorOption(WhereClauseConnector connector)
    {
        Connector = connector;
        DisplayName = connector.ToString().ToUpperInvariant();
    }

    public WhereClauseConnector Connector { get; }
    public string DisplayName { get; }
}

public static class WhereClauseFactory
{
    public static WhereClauseDataType GetDataType(DbColumn column) => column.DataType.ToLowerInvariant() switch
    {
        "bigint" or "decimal" or "double" or "float" or "int" or "money" or "numeric" or "real" or "smallint" or "smallmoney" or "tinyint" => WhereClauseDataType.Numeric,
        "char" or "nchar" or "ntext" or "nvarchar" or "text" or "varchar" or "binary" or "varbinary" or "json" or "uniqueidentifier" => WhereClauseDataType.String,
        "date" or "datetime" or "datetime2" or "datetimeoffset" or "smalldatetime" => WhereClauseDataType.Date,
        "bit" => WhereClauseDataType.Bit,
        _ => WhereClauseDataType.Unsupported
    };

    public static IReadOnlyList<WhereOperatorOption> GetApplicableOperators(DbColumn column)
    {
        List<WhereOperatorOption> operators = GetDataType(column) switch
        {
            WhereClauseDataType.Numeric =>
            [
                new WhereOperatorOption(WhereClauseOperator.Equals, "=", false),
                new WhereOperatorOption(WhereClauseOperator.LessThan, "<", false),
                new WhereOperatorOption(WhereClauseOperator.GreaterThan, ">", false),
                new WhereOperatorOption(WhereClauseOperator.LessThanOrEqual, "<=", false),
                new WhereOperatorOption(WhereClauseOperator.GreaterThanOrEqual, ">=", false),
                new WhereOperatorOption(WhereClauseOperator.NotEqual, "<>", false)
            ],
            WhereClauseDataType.String =>
            [
                new WhereOperatorOption(WhereClauseOperator.Equals, "=", false),
                new WhereOperatorOption(WhereClauseOperator.Like, "LIKE", false),
                new WhereOperatorOption(WhereClauseOperator.NotLike, "NOT LIKE", false)
            ],
            WhereClauseDataType.Date =>
            [
                new WhereOperatorOption(WhereClauseOperator.Equals, "=", false),
                new WhereOperatorOption(WhereClauseOperator.Between, "BETWEEN", true)
            ],
            WhereClauseDataType.Bit =>
            [
                new WhereOperatorOption(WhereClauseOperator.Equals, "=", false),
                new WhereOperatorOption(WhereClauseOperator.NotEqual, "<>", false)
            ],
            _ => []
        };

        if (!column.IsNullable)
            return operators;

        return operators.Concat([
            new WhereOperatorOption(WhereClauseOperator.IsNull, "IS NULL", false),
            new WhereOperatorOption(WhereClauseOperator.IsNotNull, "IS NOT NULL", false)
        ]).ToList();
    }
}

public class WhereClauseViewModel : ViewModelBase
{
    private WhereDropdownOption? _selectedColumn;
    private WhereOperatorOption? _selectedOperator;
    private string? _value;
    private string? _betweenValue;
    private WhereClauseConnector _connector;
    private bool _hasValidationIssue;

    public WhereClauseViewModel(
        IReadOnlyList<WhereDropdownOption> columns,
        WhereClauseConnector connector,
        int index)
    {
        _connector = connector;
        Index = index;
        Columns = new ObservableCollection<WhereDropdownOption>(columns);
        Operators = new ObservableCollection<WhereOperatorOption>();
    }

    public int Index { get; set; }
    public bool IsFirst => Index == 0;
    public bool IsNullable => SelectedColumn?.Column?.Column.IsNullable ?? false;
    public bool UsesBetween => SelectedOperator?.UsesBetween ?? false;
    public bool RequiresValue => SelectedColumn is not null &&
                                  SelectedOperator is not null &&
                                  SelectedOperator.Operator is not (WhereClauseOperator.IsNull or WhereClauseOperator.IsNotNull);

    public ObservableCollection<WhereDropdownOption> Columns { get; }
    public ObservableCollection<WhereOperatorOption> Operators { get; }

    public WhereDropdownOption? SelectedColumn
    {
        get => _selectedColumn;
        set
        {
            if (value?.IsHeader == true)
                return;

            if (SetProperty(ref _selectedColumn, value))
            {
                RefreshOperators();
                OnPropertyChanged(nameof(IsNullable));
                OnPropertyChanged(nameof(RequiresValue));
            }
        }
    }

    public WhereOperatorOption? SelectedOperator
    {
        get => _selectedOperator;
        set
        {
            if (SetProperty(ref _selectedOperator, value))
            {
                OnPropertyChanged(nameof(UsesBetween));
                OnPropertyChanged(nameof(RequiresValue));
                UpdateValidationIssue();
            }
        }
    }

    public string? Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
                UpdateValidationIssue();
        }
    }

    public string? BetweenValue
    {
        get => _betweenValue;
        set
        {
            if (SetProperty(ref _betweenValue, value))
                UpdateValidationIssue();
        }
    }

    public WhereClauseConnector Connector
    {
        get => _connector;
        set
        {
            if (SetProperty(ref _connector, value))
            {
                OnPropertyChanged(nameof(IsAnd));
                OnPropertyChanged(nameof(IsOr));
            }
        }
    }

    public bool IsAnd
    {
        get => Connector == WhereClauseConnector.And;
        set
        {
            if (value)
                Connector = WhereClauseConnector.And;
        }
    }

    public bool IsOr
    {
        get => Connector == WhereClauseConnector.Or;
        set
        {
            if (value)
                Connector = WhereClauseConnector.Or;
        }
    }

    public bool HasValidationIssue
    {
        get => _hasValidationIssue;
        private set => SetProperty(ref _hasValidationIssue, value);
    }

    public bool IsComplete =>
        SelectedColumn is not null &&
        SelectedOperator is not null &&
        SelectedOperator.Operator is WhereClauseOperator.IsNull or WhereClauseOperator.IsNotNull ||
        TryBuildClause() is not null;

    public QueryWhereClause? BuildClause()
    {
        var clause = TryBuildClause();
        HasValidationIssue = clause is null;
        return clause;
    }

    public void RefreshIndexes(int index)
    {
        Index = index;
        OnPropertyChanged(nameof(IsFirst));
    }

    public void UpdateColumns(IReadOnlyList<WhereDropdownOption> columns)
    {
        var selectedColumnName = SelectedColumn?.DisplayName;
        Columns.Clear();

        foreach (var column in columns)
            Columns.Add(column);

        SelectedColumn = Columns.FirstOrDefault(column => column.Column is not null && column.DisplayName == selectedColumnName);
    }

    private QueryWhereClause? TryBuildClause()
    {
        if (SelectedColumn is not { } selectedColumn || SelectedOperator is not { } selectedOperator)
            return null;

        if (selectedColumn.Column is not { } selectedColumnOption)
            return null;

        if (selectedOperator.Operator is WhereClauseOperator.IsNull or WhereClauseOperator.IsNotNull)
            return new QueryWhereClause
            {
                Table = selectedColumnOption.Table,
                Column = selectedColumnOption.Column.Name,
                DataType = WhereClauseFactory.GetDataType(selectedColumnOption.Column),
                Operator = selectedOperator.Operator,
                Value = null,
                BetweenValue = null,
                Connector = Connector
            };

        if (!ValidateValue(selectedColumnOption.Column, selectedOperator.Operator, Value))
            return null;

        if (selectedOperator.UsesBetween && !ValidateValue(selectedColumnOption.Column, selectedOperator.Operator, BetweenValue))
            return null;

        return new QueryWhereClause
        {
                Table = selectedColumnOption.Table,
                Column = selectedColumnOption.Column.Name,
                DataType = WhereClauseFactory.GetDataType(selectedColumnOption.Column),
            Operator = selectedOperator.Operator,
            Value = Value,
            BetweenValue = selectedOperator.UsesBetween ? BetweenValue : null,
            Connector = Connector
        };
    }

    private void RefreshOperators()
    {
        var selected = _selectedOperator?.Operator;
        Operators.Clear();

        if (SelectedColumn?.Column is not { } selectedColumn)
            return;

        foreach (var option in WhereClauseFactory.GetApplicableOperators(selectedColumn.Column))
        {
            Operators.Add(option);
            if (option.Operator == selected)
                SelectedOperator = option;
        }

        if (_selectedOperator is null)
            SelectedOperator = Operators.FirstOrDefault();
        OnPropertyChanged(nameof(IsNullable));
    }

    private void UpdateValidationIssue()
    {
        HasValidationIssue = SelectedColumn is not null &&
                             SelectedOperator is not null &&
                             SelectedOperator.Operator is not (WhereClauseOperator.IsNull or WhereClauseOperator.IsNotNull) &&
                             string.IsNullOrWhiteSpace(Value);
    }

    private bool ValidateValue(DbColumn column, WhereClauseOperator @operator, string? value)
    {
        if (@operator is WhereClauseOperator.IsNull or WhereClauseOperator.IsNotNull)
            return true;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var dataType = WhereClauseFactory.GetDataType(column);

        switch (dataType)
        {
            case WhereClauseDataType.Numeric:
                return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
            case WhereClauseDataType.Bit:
                return TryParseBit(value, out _);
            case WhereClauseDataType.Date:
                return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            case WhereClauseDataType.String:
                return true;
            case WhereClauseDataType.Unsupported:
            default:
                return false;
        }
    }

    private static bool TryParseBit(string value, out bool parsed) =>
        bool.TryParse(value, out parsed) ||
        (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && numeric is 0 or 1 && (parsed = numeric == 1));
}
