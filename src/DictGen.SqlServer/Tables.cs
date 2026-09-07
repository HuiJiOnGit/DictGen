using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using DictGen.Abstractions;
using DictGen.Abstractions.Models;
using Microsoft.Data.SqlClient;

namespace DictGen.SqlServer;

/// <summary>
/// SqlServerSchemaProvider 的表相关 partial:清单、字段、索引、外键。
/// </summary>
internal sealed partial class SqlServerSchemaProvider
{
    // ======== 表生产者(流式) ========

    private async Task ProduceTablesAsync(
        Channel<SchemaObject> channel, IProgress<SchemaProgress>? progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        // 清单/字段/索引/外键四个全量查询互不依赖(后三者连清单都不依赖),并行把该路耗时从求和压到最大值
        async Task<T> TimedAsync<T>(string label, Func<SqlConnection, CancellationToken, Task<T>> action)
        {
            var qsw = Stopwatch.StartNew();
            var result = await WithConnAsync(action, ct);
            Logger.LogInformation("⏱ {Label} 耗时 {Elapsed:F1}s", label, qsw.Elapsed.TotalSeconds);
            return result;
        }

        var listTask = TimedAsync("表清单", ReadTableListAsync);
        var colTask = TimedAsync("全量字段", ReadAllColumnsAsync);
        var ixTask = TimedAsync("全量索引", ReadAllIndexesAsync);
        var fkTask = TimedAsync("全量外键", ReadAllForeignKeysAsync);

        var tables = await listTask;
        if (tables.Count == 0)
        {
            await Task.WhenAll(colTask, ixTask, fkTask); // 观察已启动任务,避免未观察异常
            return;
        }

        Logger.LogInformation("📋 表清单: {Count} 张", tables.Count);
        progress?.Report(new SchemaProgress(SchemaReadStage.ReadingTables, tables.Count, tables.Count, 15));

        await Task.WhenAll(colTask, ixTask, fkTask);
        var colDict = await colTask;
        var ixDict = await ixTask;
        var fkDict = await fkTask;

        Logger.LogInformation("🔗 字段 {Cols} 条, 索引 {Ix} 条, 外键 {Fk} 条, 耗时 {Elapsed:F1}s",
            colDict.Values.Sum(c => c.Count), ixDict.Values.Sum(i => i.Count), fkDict.Values.Sum(f => f.Count),
            sw.Elapsed.TotalSeconds);

        // 字典键必须带 schema:多 schema 下同名表(如 dbo.Users / sales.Users)仅按表名会数据错配
        var tableKeyOf = (TableInfo t) => $"{t.Schema}.{t.Name}";
        for (var i = 0; i < tables.Count; i++)
        {
            var t = tables[i];
            t.Columns = colDict.GetValueOrDefault(tableKeyOf(t), []);
            t.Indexes = ixDict.GetValueOrDefault(tableKeyOf(t), []);
            t.ForeignKeys = fkDict.GetValueOrDefault(tableKeyOf(t), []);
            await channel.Writer.WriteAsync(new SchemaObject { Kind = SchemaObjectKind.Table, Table = t }, ct);

            if ((i + 1) % 50 == 0 || i == tables.Count - 1)
            {
                Logger.LogInformation("📊 表 {Done}/{Total}", i + 1, tables.Count);
                progress?.Report(new SchemaProgress(SchemaReadStage.ReadingTables, i + 1, tables.Count,
                    15 + 65 * (i + 1) / tables.Count));
            }
        }
        Logger.LogInformation("📊 表生产者完成,耗时 {Elapsed:F1}s", sw.Elapsed.TotalSeconds);
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

    /// <summary>
    /// 全量字段:整包 FOR JSON + COMPRESS 单值往返,替代数千行宽行的流式传输
    /// (行内容逐字段一致,仅载体不同);HASH JOIN 强制各目录表一次扫描,
    /// 避免逐行嵌套探测在小缓存池/低 IOPS 磁盘上的随机 IO。
    /// </summary>
    private async Task<Dictionary<string, List<ColumnInfo>>> ReadAllColumnsAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = $"""
            SELECT COMPRESS((
                SELECT t.name AS [tn], c.name AS [n], c.column_id AS [ord],
                    UPPER(tp.name) AS [dt],
                    {DataTypeExpr("c")} AS [dtf],
                    c.is_nullable AS [nu], c.is_identity AS [id], c.is_computed AS [cp],
                    c.collation_name AS [co], ep.value AS [dn], dc.definition AS [de],
                    CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS [pk],
                    sch.name AS [sn]
                FROM sys.columns c
                INNER JOIN sys.tables t ON c.object_id = t.object_id
                INNER JOIN sys.schemas sch ON t.schema_id = sch.schema_id
                INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                LEFT JOIN sys.extended_properties ep ON ep.major_id=c.object_id AND ep.minor_id=c.column_id AND ep.name='MS_Description'
                LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id
                LEFT JOIN (
                    SELECT ic.object_id, ic.column_id
                    FROM sys.indexes i
                    INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                    WHERE i.is_primary_key=1
                ) pk ON pk.object_id=c.object_id AND pk.column_id=c.column_id
                ORDER BY sch.name, t.name, c.column_id
                FOR JSON PATH
            ))
            OPTION (HASH JOIN, FORCE ORDER)
            """;

        await using var cmd = Cmd(conn, sql);
        var qsw = Stopwatch.StartNew();
        var payload = await cmd.ExecuteScalarAsync(ct) as byte[];
        var qElapsed = qsw.Elapsed.TotalSeconds;

        // FOR JSON 在零行时返回 NULL(COMPRESS(NULL) 同样为 NULL),直接得空结果
        var result = new Dictionary<string, List<ColumnInfo>>();
        if (payload is null || payload.Length == 0) return result;

        var psw = Stopwatch.StartNew();
        using var doc = JsonDocument.Parse(Encoding.Unicode.GetString(DecompressGzip(payload)));
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var col = new ColumnInfo
            {
                Name = el.GetProperty("n").GetString()!,
                Ordinal = el.GetProperty("ord").GetInt32(),
                DataType = el.GetProperty("dt").GetString()!,
                DataTypeFull = el.GetProperty("dtf").GetString()!,
                IsNullable = el.GetProperty("nu").GetBoolean(),
                IsIdentity = el.GetProperty("id").GetBoolean(),
                IsComputed = el.GetProperty("cp").GetBoolean(),
                Collation = TryStr(el, "co"),
                Description = TryStr(el, "dn"),
                DefaultValue = TryStr(el, "de"),
                IsPrimaryKey = el.GetProperty("pk").GetInt32() == 1,
            };
            var tn = $"{TryStr(el, "sn")}.{el.GetProperty("tn").GetString()}";
            if (!result.TryGetValue(tn, out var l)) result[tn] = l = [];
            l.Add(col);
        }
        Logger.LogInformation("⏱ 全量字段 {Rows} 行, 压缩包 {Mb:F2} MB, 服务端 {Q:F1}s, 解压解析 {P:F1}s",
            doc.RootElement.GetArrayLength(), payload.Length / 1024.0 / 1024.0, qElapsed, psw.Elapsed.TotalSeconds);
        return result;
    }

    /// <summary>JSON 元素中可空字符串字段的容错读取(FOR JSON 默认省略 null 字段)。</summary>
    private static string? TryStr(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private async Task<Dictionary<string, List<IndexInfo>>> ReadAllIndexesAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = """
            SELECT OBJECT_NAME(i.object_id), i.name, i.is_unique, i.is_primary_key, i.type_desc, i.filter_definition,
                STRING_AGG(CASE WHEN ic.is_included_column=0 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal),
                STRING_AGG(CASE WHEN ic.is_included_column=1 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal),
                OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            LEFT JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            LEFT JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.type IN (1,2) AND i.name IS NOT NULL
            GROUP BY i.object_id, i.name, i.is_unique, i.is_primary_key, i.type_desc, i.filter_definition
            ORDER BY OBJECT_SCHEMA_NAME(i.object_id), OBJECT_NAME(i.object_id)
            """;
        var result = new Dictionary<string, List<IndexInfo>>();
        await using var cmd = Cmd(conn, sql);
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
            var tn = $"{r.GetString(8)}.{r.GetString(0)}";
            if (!result.TryGetValue(tn, out var l)) result[tn] = l = [];
            l.Add(idx);
        }
        return result;
    }

    private async Task<Dictionary<string, List<ForeignKeyInfo>>> ReadAllForeignKeysAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = """
            SELECT OBJECT_NAME(fk.parent_object_id), fk.name,
                OBJECT_SCHEMA_NAME(fk.referenced_object_id)+'.'+OBJECT_NAME(fk.referenced_object_id),
                STRING_AGG(COL_NAME(fkc.parent_object_id,fkc.parent_column_id), ', ') WITHIN GROUP (ORDER BY fkc.constraint_column_id),
                STRING_AGG(COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id), ', ') WITHIN GROUP (ORDER BY fkc.constraint_column_id),
                fk.delete_referential_action_desc, fk.update_referential_action_desc,
                OBJECT_SCHEMA_NAME(fk.parent_object_id) AS SchemaName
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
            GROUP BY fk.parent_object_id, fk.referenced_object_id, fk.name, fk.delete_referential_action_desc, fk.update_referential_action_desc
            """;
        var result = new Dictionary<string, List<ForeignKeyInfo>>();
        await using var cmd = Cmd(conn, sql);
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
            var tn = $"{r.GetString(7)}.{r.GetString(0)}";
            if (!result.TryGetValue(tn, out var l)) result[tn] = l = [];
            l.Add(fk);
        }
        return result;
    }
}
