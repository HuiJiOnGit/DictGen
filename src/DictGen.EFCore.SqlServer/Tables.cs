using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using DictGen.Abstractions;
using System.Threading.Channels;
using DictGen.Abstractions.Models;
using Microsoft.Data.SqlClient;

namespace DictGen.EFCore.SqlServer;

/// <summary>
/// SqlServerSchemaProvider 的表相关 partial:清单、字段、索引、外键。
/// </summary>
internal sealed partial class SqlServerSchemaProvider
{
    // ======== 表生产者(流式) ========

    private async Task ProduceTablesAsync(
        Channel<SchemaObject> channel, IProgress<SchemaProgress>? progress, CancellationToken ct)
    {
        var tables = await WithConnAsync(ReadTableListAsync, ct);
        if (tables.Count == 0) return;

        Logger.LogInformation("📋 表清单: {Count} 张", tables.Count);
        progress?.Report(new SchemaProgress(SchemaReadStage.ReadingTables, tables.Count, tables.Count, 15));

        var colDict = await WithConnAsync(ReadAllColumnsAsync, ct);
        var ixDict = await WithConnAsync(ReadAllIndexesAsync, ct);
        var fkDict = await WithConnAsync(ReadAllForeignKeysAsync, ct);

        Logger.LogInformation("🔗 字段 {Cols} 条, 索引 {Ix} 条, 外键 {Fk} 条",
            colDict.Values.Sum(c => c.Count), ixDict.Values.Sum(i => i.Count), fkDict.Values.Sum(f => f.Count));

        for (var i = 0; i < tables.Count; i++)
        {
            var t = tables[i];
            t.Columns = colDict.GetValueOrDefault(t.Name, []);
            t.Indexes = ixDict.GetValueOrDefault(t.Name, []);
            t.ForeignKeys = fkDict.GetValueOrDefault(t.Name, []);
            await channel.Writer.WriteAsync(new SchemaObject { Kind = SchemaObjectKind.Table, Table = t }, ct);

            if ((i + 1) % 50 == 0 || i == tables.Count - 1)
            {
                Logger.LogInformation("📊 表 {Done}/{Total}", i + 1, tables.Count);
                progress?.Report(new SchemaProgress(SchemaReadStage.ReadingTables, i + 1, tables.Count,
                    15 + 65 * (i + 1) / tables.Count));
            }
        }
    }

    private async Task<List<TableInfo>> ReadTableListAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = """
            SELECT s.name, t.name, ep.value, ps.row_count, t.create_date, t.modify_date
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
            OUTER APPLY (SELECT SUM(ddps.row_count) AS row_count FROM sys.dm_db_partition_stats ddps WHERE ddps.object_id = t.object_id AND ddps.index_id < 2) ps
            ORDER BY s.name, t.name
            """;
        if (Options.SchemaFilter.Count > 0)
        {
            var q = string.Join(",", Options.SchemaFilter.Select(s => $"'{s.Replace("'", "''")}'"));
            sql = sql.Replace("ORDER BY s.name, t.name",
                $"WHERE s.name IN ({q}) ORDER BY s.name, t.name");
        }

        var list = new List<TableInfo>();
        await using var cmd = Cmd(conn, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new TableInfo
            {
                Schema = r.GetString(0), Name = r.GetString(1),
                Description = r.IsDBNull(2) ? null : r.GetString(2),
                RowCount = r.IsDBNull(3) ? null : (long?)r.GetInt64(3),
                CreatedAt = r.IsDBNull(4) ? null : (DateTime?)r.GetDateTime(4),
                ModifiedAt = r.IsDBNull(5) ? null : (DateTime?)r.GetDateTime(5),
            });
        return list;
    }

    private async Task<Dictionary<string, List<ColumnInfo>>> ReadAllColumnsAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = $"""
            SELECT OBJECT_NAME(c.object_id), c.name, c.column_id,
                UPPER(tp.name) AS DT,
                {DataTypeExpr("c")} AS DTF,
                c.is_nullable, COLUMNPROPERTY(c.object_id,c.name,'IsIdentity'),
                COLUMNPROPERTY(c.object_id,c.name,'IsComputed'),
                c.collation_name, ep.value, dc.definition,
                CASE WHEN EXISTS (
                    SELECT 1 FROM sys.indexes i
                    INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                    WHERE i.object_id=c.object_id AND i.is_primary_key=1 AND ic.column_id=c.column_id
                ) THEN 1 ELSE 0 END AS PK
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
            LEFT JOIN sys.extended_properties ep ON ep.major_id=c.object_id AND ep.minor_id=c.column_id AND ep.name='MS_Description'
            LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id
            ORDER BY OBJECT_NAME(c.object_id), c.column_id
            """;

        var result = new Dictionary<string, List<ColumnInfo>>();
        await using var cmd = Cmd(conn, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var col = new ColumnInfo
            {
                Name = r.GetString(1), Ordinal = r.GetInt32(2),
                DataType = r.GetString(3), DataTypeFull = r.GetString(4),
                IsNullable = r.GetBoolean(5), IsIdentity = r.GetInt32(6) == 1,
                IsComputed = r.GetInt32(7) == 1,
                Collation = r.IsDBNull(8) ? null : r.GetString(8),
                Description = r.IsDBNull(9) ? null : r.GetString(9),
                DefaultValue = r.IsDBNull(10) ? null : r.GetString(10),
                IsPrimaryKey = r.GetInt32(11) == 1,
            };
            var tn = r.GetString(0);
            if (!result.TryGetValue(tn, out var l)) result[tn] = l = [];
            l.Add(col);
        }
        return result;
    }

    private static async Task<Dictionary<string, List<IndexInfo>>> ReadAllIndexesAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = """
            SELECT OBJECT_NAME(i.object_id), i.name, i.is_unique, i.is_primary_key, i.type_desc, i.filter_definition,
                STRING_AGG(CASE WHEN ic.is_included_column=0 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal),
                STRING_AGG(CASE WHEN ic.is_included_column=1 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            LEFT JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            LEFT JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.type IN (1,2) AND i.name IS NOT NULL
            GROUP BY i.object_id, i.name, i.is_unique, i.is_primary_key, i.type_desc, i.filter_definition
            ORDER BY OBJECT_NAME(i.object_id)
            """;
        var result = new Dictionary<string, List<IndexInfo>>();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var idx = new IndexInfo
            {
                Name = r.GetString(1), IsUnique = r.GetBoolean(2),
                IsPrimaryKey = r.GetBoolean(3), Type = r.GetString(4),
                Filter = r.IsDBNull(5) ? null : r.GetString(5),
                Columns = r.IsDBNull(6) ? [] : r.GetString(6).Split(", "),
                IncludedColumns = r.IsDBNull(7) ? [] : r.GetString(7).Split(", "),
            };
            var tn = r.GetString(0);
            if (!result.TryGetValue(tn, out var l)) result[tn] = l = [];
            l.Add(idx);
        }
        return result;
    }

    private static async Task<Dictionary<string, List<ForeignKeyInfo>>> ReadAllForeignKeysAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = """
            SELECT OBJECT_NAME(fk.parent_object_id), fk.name,
                OBJECT_SCHEMA_NAME(fk.referenced_object_id)+'.'+OBJECT_NAME(fk.referenced_object_id),
                STRING_AGG(COL_NAME(fkc.parent_object_id,fkc.parent_column_id), ', ') WITHIN GROUP (ORDER BY fkc.constraint_column_id),
                STRING_AGG(COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id), ', ') WITHIN GROUP (ORDER BY fkc.constraint_column_id),
                fk.delete_referential_action_desc, fk.update_referential_action_desc
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
            GROUP BY fk.parent_object_id, fk.referenced_object_id, fk.name, fk.delete_referential_action_desc, fk.update_referential_action_desc
            """;
        var result = new Dictionary<string, List<ForeignKeyInfo>>();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var fc = r.GetString(3).Split(", "); var rc = r.GetString(4).Split(", ");
            var fk = new ForeignKeyInfo
            {
                Name = r.GetString(1), ReferencedTable = r.GetString(2),
                Columns = fc.Zip(rc, (c, r2) => new ForeignKeyColumn(c, r2)).ToArray(),
                OnDeleteAction = r.IsDBNull(5) ? null : r.GetString(5),
                OnUpdateAction = r.IsDBNull(6) ? null : r.GetString(6),
            };
            var tn = r.GetString(0);
            if (!result.TryGetValue(tn, out var l)) result[tn] = l = [];
            l.Add(fk);
        }
        return result;
    }

    // ========== 非流式向后兼容 ==========

    private async Task<List<TableInfo>> FetchTablesAsync(SqlConnection conn, CancellationToken ct)
    {
        var tables = await ReadTableListAsync(conn, ct);
        if (tables.Count == 0) return tables;
        var cols = await ReadAllColumnsAsync(conn, ct);
        var ix = await ReadAllIndexesAsync(conn, ct);
        var fk = await ReadAllForeignKeysAsync(conn, ct);
        foreach (var t in tables)
        {
            t.Columns = cols.GetValueOrDefault(t.Name, []);
            t.Indexes = ix.GetValueOrDefault(t.Name, []);
            t.ForeignKeys = fk.GetValueOrDefault(t.Name, []);
        }
        return tables;
    }
}
