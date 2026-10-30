using Avalonia;
using Avalonia.Media;

namespace DBDiver.Models;

public enum QueryBuilderJoinType
{
    Inner,
    Left,
    Right,
    Full
}

public class QueryBuilderSettings
{
    public QueryBuilderJoinType DefaultJoinType { get; set; } = QueryBuilderJoinType.Left;
    public string WhereTableHeaderColor { get; set; } = GetResourceColor("QueryBuilderWhereTableHeaderBrush");
    public string AddClauseButtonColor { get; set; } = GetResourceColor("QueryBuilderAddClauseButtonBrush");
    public string GroupDepth1Color { get; set; } = GetResourceColor("QueryBuilderGroupDepth1Brush");
    public string GroupDepth2Color { get; set; } = GetResourceColor("QueryBuilderGroupDepth2Brush");
    public string GroupDepth3Color { get; set; } = GetResourceColor("QueryBuilderGroupDepth3Brush");
    public string ValidationIssueBorderColor { get; set; } = GetResourceColor("QueryBuilderValidationIssueBorderBrush");
    public string WhereTableHeaderBackgroundColor { get; set; } = GetResourceColor("QueryBuilderWhereTableHeaderBackgroundBrush");

    public string InnerColor { get; set; } = GetResourceColor("QueryBuilderInnerJoinColorBrush");
    public string LeftColor { get; set; } = GetResourceColor("QueryBuilderLeftJoinColorBrush");
    public string RightColor { get; set; } = GetResourceColor("QueryBuilderRightJoinColorBrush");
    public string FullColor { get; set; } = GetResourceColor("QueryBuilderFullJoinColorBrush");

    private static string GetResourceColor(string resourceKey)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var resource) == true
            && resource is SolidColorBrush brush)
            return brush.Color.ToString();

        return string.Empty;
    }
}
