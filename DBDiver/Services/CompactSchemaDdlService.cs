using System.Text;
using DBDiver.Models;

namespace DBDiver.Services;

public class CompactSchemaDdlService
{
    public string Build(DbSchema schema)
    {
        var ddl = new StringBuilder();

        foreach (var table in schema.Tables.OrderBy(table => table.QualifiedName, StringComparer.OrdinalIgnoreCase))
        {
            ddl.Append(table.QualifiedName);
            ddl.Append('(');

            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var column = table.Columns[columnIndex];
                if (columnIndex > 0)
                    ddl.Append(", ");

                ddl.Append(column.Name);
                ddl.Append(' ');
                ddl.Append(column.DataType);
                if (table.PrimaryKeys.Contains(column.Name, StringComparer.OrdinalIgnoreCase))
                    ddl.Append(" PK");
            }

            var relationships = schema.Relationships
                .Where(relationship => relationship.SourceTable.Equals(table.QualifiedName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(relationship => relationship.SourceColumn, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var relationship in relationships)
            {
                ddl.Append(", ");
                ddl.Append(relationship.SourceColumn);
                ddl.Append(" FK->");
                ddl.Append(relationship.TargetTable);
            }

            ddl.Append(')');
        }

        var ddlString = ddl.ToString();
        return ddlString; 
    }
}
