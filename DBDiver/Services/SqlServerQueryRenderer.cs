using System.Text;
using DBDiver.Models;

namespace DBDiver.Services;

public class SqlServerQueryRenderer
{
    public string Render(QueryModel query)
    {
        if (query.Tables.Count == 0)
            return string.Empty;

        var sql = new StringBuilder();
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

        return sql.ToString();
    }

    private static void AppendTable(StringBuilder sql, QueryTable table)
    {
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
