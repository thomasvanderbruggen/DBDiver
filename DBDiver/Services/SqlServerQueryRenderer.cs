using System.Text;
using DBDiver.Models;

namespace DBDiver.Services;

public class SqlServerQueryRenderer
{
    public string Render(QueryModel query)
    {
        return Render(query, useParameters: false, out _);
    }

    public string Render(QueryModel query, out IReadOnlyList<QueryParameter> parameters)
    {
        return Render(query, useParameters: true, out parameters);
    }

    public string Render(QueryModel query, bool useParameters, out IReadOnlyList<QueryParameter> parameters)
    {
        if (query.Tables.Count == 0)
        {
            parameters = [];
            return string.Empty;
        }

        var sql = new StringBuilder();
        var parameterValues = new List<QueryParameter>();
        sql.Append("SELECT * FROM ");
        AppendTable(sql, query.Tables[0]);

        foreach (var join in query.Joins)
        {
            sql.AppendLine();
            sql.Append(GetJoinKeyword(join.JoinType));
            sql.Append(" JOIN ");
            AppendTable(sql, join.RightTable);
            sql.Append(" ON ");
            AppendColumn(sql, join.LeftTable.Alias, join.LeftColumn);
            sql.Append(" = ");
            AppendColumn(sql, join.RightTable.Alias, join.RightColumn);
        }

        AppendWhereClauses(sql, query.WhereClauses, parameterValues, useParameters);
        parameters = parameterValues;
        return sql.ToString();
    }

    private static void AppendWhereClauses(
        StringBuilder sql,
        IReadOnlyList<QueryWhereClause> clauses,
        List<QueryParameter> parameters,
        bool useParameters)
    {
        if (clauses.Count == 0)
            return;

        sql.AppendLine();
        sql.Append("WHERE ");

        for (var index = 0; index < clauses.Count; index++)
        {
            if (index > 0)
                sql.Append(clauses[index].Connector == WhereClauseConnector.Or ? " OR " : " AND ");

            var clause = clauses[index];
            AppendColumn(sql, clause.Table.Alias, clause.Column);

            switch (clause.Operator)
            {
                case WhereClauseOperator.IsNull:
                    sql.Append(" IS NULL");
                    break;
                case WhereClauseOperator.IsNotNull:
                    sql.Append(" IS NOT NULL");
                    break;
                case WhereClauseOperator.Between:
                {
                    if (useParameters)
                    {
                        var startName = GetParameterName(parameters.Count);
                        parameters.Add(new QueryParameter(startName, clause.Value ?? string.Empty));
                        var endName = GetParameterName(parameters.Count);
                        parameters.Add(new QueryParameter(endName, clause.BetweenValue ?? string.Empty));
                        sql.Append($" BETWEEN {startName} AND {endName}");
                    }
                    else
                    {
                        sql.Append(" BETWEEN ")
                           .Append(GetSqlValue(clause.Value, clause.DataType))
                           .Append(" AND ")
                           .Append(GetSqlValue(clause.BetweenValue, clause.DataType));
                    }

                    break;
                }
                default:
                {
                    if (useParameters)
                    {
                        var parameterName = GetParameterName(parameters.Count);
                        parameters.Add(new QueryParameter(parameterName, clause.Value ?? string.Empty));
                        sql.Append(' ').Append(GetOperator(clause.Operator)).Append(' ').Append(parameterName);
                    }
                    else
                    {
                        sql.Append(' ')
                           .Append(GetOperator(clause.Operator))
                           .Append(' ')
                           .Append(GetSqlValue(clause.Value, clause.DataType));
                    }

                    break;
                }
            }
        }
    }

    private static string GetSqlValue(string? value, WhereClauseDataType dataType)
    {
        var valueText = value ?? string.Empty;

        return dataType switch
        {
            WhereClauseDataType.Numeric => valueText,
            WhereClauseDataType.Bit => valueText.Equals("true", StringComparison.OrdinalIgnoreCase) ? "1" : valueText.Equals("false", StringComparison.OrdinalIgnoreCase) ? "0" : valueText,
            WhereClauseDataType.Date or WhereClauseDataType.String => $"'{valueText.Replace("'", "''", StringComparison.Ordinal)}'",
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported WHERE data type")
        };
    }

    private static string GetOperator(WhereClauseOperator whereOperator) => whereOperator switch
    {
        WhereClauseOperator.Equals => "=",
        WhereClauseOperator.LessThan => "<",
        WhereClauseOperator.GreaterThan => ">",
        WhereClauseOperator.LessThanOrEqual => "<=",
        WhereClauseOperator.GreaterThanOrEqual => ">=",
        WhereClauseOperator.NotEqual => "<>",
        WhereClauseOperator.Like => "LIKE",
        WhereClauseOperator.NotLike => "NOT LIKE",
        _ => throw new ArgumentOutOfRangeException(nameof(whereOperator), whereOperator, "Unsupported WHERE operator")
    };

    private static string GetParameterName(int index) => $"@where{index}";

    private static void AppendTable(StringBuilder sql, QueryTable table)
    {
        if (!string.IsNullOrEmpty(table.Schema))
        {
            sql.Append('[');
            sql.Append(table.Schema);
            sql.Append("].");
        }

        sql.Append('[');
        sql.Append(table.Name);
        sql.Append("] AS [");
        sql.Append(table.Alias);
        sql.Append(']');
    }

    private static string GetJoinKeyword(QueryJoinType joinType) => joinType switch
    {
        QueryJoinType.Inner => "INNER",
        QueryJoinType.Left => "LEFT",
        QueryJoinType.Right => "RIGHT",
        QueryJoinType.Full => "FULL",
        _ => throw new ArgumentOutOfRangeException(nameof(joinType), joinType, "Unsupported join type")
    };

    private static void AppendColumn(StringBuilder sql, string alias, string column)
    {
        sql.Append('[');
        sql.Append(alias);
        sql.Append("].[");
        sql.Append(column);
        sql.Append(']');
    }
}
