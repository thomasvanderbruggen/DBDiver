namespace DBDiver.Services;

public static class SqlServerSchemaQueries
{
    public const string Schema = """
        SELECT
            s.name                  AS [schema],
            t.name                  AS [table],
            c.column_id             AS column_id,
            c.name                  AS [column],
            ty.name                 AS data_type,
            TYPE_NAME(ty.system_type_id) AS underlying_data_type,
            c.is_nullable           AS is_nullable,
            CASE WHEN pkc.column_id IS NOT NULL THEN 1 ELSE 0 END AS is_pk,
            pkc.key_ordinal         AS pk_ordinal,
            fk.name                 AS fk_name,
            fkc.constraint_column_id AS fk_ordinal,
            rs.name                 AS fk_target_schema,
            rt.name                 AS fk_target_table,
            rc.name                 AS fk_target_column
        FROM sys.tables t
        JOIN sys.schemas s  ON s.schema_id = t.schema_id
        JOIN sys.columns c  ON c.object_id = t.object_id
        JOIN sys.types ty   ON ty.user_type_id = c.user_type_id
        LEFT JOIN (
            SELECT ic.object_id, ic.column_id, ic.key_ordinal
            FROM sys.indexes i
            JOIN sys.index_columns ic
              ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.is_primary_key = 1
        ) pkc ON pkc.object_id = c.object_id AND pkc.column_id = c.column_id
        LEFT JOIN sys.foreign_key_columns fkc
          ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
        LEFT JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
        LEFT JOIN sys.tables rt  ON rt.object_id = fkc.referenced_object_id
        LEFT JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
        LEFT JOIN sys.columns rc
          ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
        ORDER BY s.name, t.name, c.column_id, fk.name;
        """;
}
