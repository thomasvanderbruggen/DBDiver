using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
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
        "char" or "varchar" => WhereClauseDataType.String,
        "nchar" or "nvarchar" => WhereClauseDataType.String,
        "text" or "ntext" => WhereClauseDataType.LegacyText,
        "binary" or "varbinary" => WhereClauseDataType.Binary,
        "rowversion" or "timestamp" => WhereClauseDataType.RowVersion,
        "date" => WhereClauseDataType.Date,
        "time" => WhereClauseDataType.Time,
        "datetime" or "datetime2" or "smalldatetime" => WhereClauseDataType.DateTime,
        "datetimeoffset" => WhereClauseDataType.DateTimeOffset,
        "bit" => WhereClauseDataType.Bit,
        "uniqueidentifier" => WhereClauseDataType.Guid,
        "hierarchyid" => WhereClauseDataType.HierarchyId,
        "cursor" or "geography" or "geometry" or "image" or "json" or "sql_variant" or "table" or "vector" or "xml" => WhereClauseDataType.Unsupported,
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
                new WhereOperatorOption(WhereClauseOperator.NotEqual, "<>", false),
                new WhereOperatorOption(WhereClauseOperator.Between, "BETWEEN", true)
            ],
            WhereClauseDataType.String =>
            [
                new WhereOperatorOption(WhereClauseOperator.Equals, "=", false),
                new WhereOperatorOption(WhereClauseOperator.NotEqual, "<>", false),
                new WhereOperatorOption(WhereClauseOperator.Like, "LIKE", false),
                new WhereOperatorOption(WhereClauseOperator.NotLike, "NOT LIKE", false)
            ],
            WhereClauseDataType.LegacyText =>
            [
                new WhereOperatorOption(WhereClauseOperator.Like, "LIKE", false),
                new WhereOperatorOption(WhereClauseOperator.NotLike, "NOT LIKE", false)
            ],
            WhereClauseDataType.Date or
            WhereClauseDataType.Time or
            WhereClauseDataType.DateTime or
            WhereClauseDataType.DateTimeOffset or
            WhereClauseDataType.Binary or
            WhereClauseDataType.RowVersion =>
            [
                new WhereOperatorOption(WhereClauseOperator.Equals, "=", false),
                new WhereOperatorOption(WhereClauseOperator.NotEqual, "<>", false),
                new WhereOperatorOption(WhereClauseOperator.LessThan, "<", false),
                new WhereOperatorOption(WhereClauseOperator.GreaterThan, ">", false),
                new WhereOperatorOption(WhereClauseOperator.LessThanOrEqual, "<=", false),
                new WhereOperatorOption(WhereClauseOperator.GreaterThanOrEqual, ">=", false),
                new WhereOperatorOption(WhereClauseOperator.Between, "BETWEEN", true)
            ],
            WhereClauseDataType.Guid or
            WhereClauseDataType.HierarchyId =>
            [
                new WhereOperatorOption(WhereClauseOperator.Equals, "=", false),
                new WhereOperatorOption(WhereClauseOperator.NotEqual, "<>", false),
                new WhereOperatorOption(WhereClauseOperator.LessThan, "<", false),
                new WhereOperatorOption(WhereClauseOperator.GreaterThan, ">", false),
                new WhereOperatorOption(WhereClauseOperator.LessThanOrEqual, "<=", false),
                new WhereOperatorOption(WhereClauseOperator.GreaterThanOrEqual, ">=", false)
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
    private bool _isDropTarget;

    public event Action? ValidationIssueChanged;

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }

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

    public IReadOnlyList<object> Children { get; } = [];

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
        var wasInvalid = HasValidationIssue;

        HasValidationIssue = SelectedColumn is not null &&
                             SelectedOperator is not null &&
                             SelectedOperator.Operator is not (WhereClauseOperator.IsNull or WhereClauseOperator.IsNotNull) &&
                             Value is not { Length: > 0 };

        if (wasInvalid != HasValidationIssue)
            ValidationIssueChanged?.Invoke();
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
                return IsFloatingPointNumeric(column.DataType) ?
                    double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) :
                    decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
            case WhereClauseDataType.Bit:
                return TryParseBit(value, out _);
            case WhereClauseDataType.Date:
                return DatePattern.IsMatch(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            case WhereClauseDataType.Time:
                return TimePattern.IsMatch(value) && TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            case WhereClauseDataType.DateTime:
                return DateTimePattern.IsMatch(value) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            case WhereClauseDataType.DateTimeOffset:
                return DateTimeOffsetPattern.IsMatch(value) &&
                       DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeOffset) &&
                       dateTimeOffset.Offset != TimeSpan.Zero;
            case WhereClauseDataType.Binary:
                return IsHexLiteral(value);
            case WhereClauseDataType.RowVersion:
                return IsHexLiteral(value, expectedHexDigitCount: 16);
            case WhereClauseDataType.Guid:
                return Guid.TryParseExact(value, "D", out _) ||
                       Guid.TryParseExact(value, "N", out _) ||
                       Guid.TryParseExact(value, "B", out _);
            case WhereClauseDataType.HierarchyId:
                return TryParseHierarchyId(value);
            case WhereClauseDataType.String:
            case WhereClauseDataType.LegacyText:
                return value is { Length: > 0 };
            case WhereClauseDataType.Unsupported:
            default:
                return false;
        }
    }

    private static bool TryParseBit(string value, out bool parsed) =>
        bool.TryParse(value, out parsed) ||
        (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && numeric is 0 or 1 && (parsed = numeric == 1));

    private static bool IsFloatingPointNumeric(string dataType) =>
        string.Equals(dataType, "float", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(dataType, "real", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(dataType, "double", StringComparison.OrdinalIgnoreCase);

    private static bool IsHexLiteral(string value, int? expectedHexDigitCount = null)
    {
        if (!value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return false;

        var hexDigits = value[2..];
        return hexDigits.Length > 0 &&
               (expectedHexDigitCount is null || hexDigits.Length == expectedHexDigitCount) &&
               hexDigits.All(Uri.IsHexDigit);
    }

    private static bool TryParseHierarchyId(string value)
    {
        if (value == "/")
            return true;

        if (!value.StartsWith('/') || !value.EndsWith('/') || value.Contains("//"))
            return false;

        var labels = value[1..^1].Split('/');
        return labels.Length > 0 && labels.All(TryParseHierarchyIdLabel);
    }

    private static bool TryParseHierarchyIdLabel(string label) =>
        label.Split('.').All(part => HierarchyIdNodePattern.IsMatch(part));

    private static readonly Regex DatePattern = new("^\\d{4}-\\d{2}-\\d{2}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TimePattern = new("^\\d{2}:\\d{2}:\\d{2}(?:\\.\\d{1,7})?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DateTimePattern = new("^\\d{4}-\\d{2}-\\d{2}(?:T\\d{2}:\\d{2}:\\d{2}(?:\\.\\d{1,7})?)?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DateTimeOffsetPattern = new("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:\\.\\d{1,7})?(?:Z|[+-]\\d{2}:\\d{2})$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HierarchyIdNodePattern = new("^(?:0|-?[1-9][0-9]*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
