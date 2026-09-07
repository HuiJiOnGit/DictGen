using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using DictGen.Abstractions;
using DictGen.Abstractions.Models;
using Microsoft.Data.SqlClient;

namespace DictGen.SqlServer;

/// <summary>
/// SqlServerSchemaProvider 的视图相关 partial。
/// </summary>
internal sealed partial class SqlServerSchemaProvider
{
    // ======== 视图生产者(流式) ========

    private async Task ProduceViewsAsync(
        Channel<SchemaObject> channel, IProgress<SchemaProgress>? progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var views = await WithConnAsync(ReadViewListAsync, ct);
        if (views.Count == 0) return;
        Logger.LogInformation("👁️ 视图清单: {Count} 个", views.Count);

        var ids = views.Select(v => v.ObjectId).ToList();
        var defTask = Options.IncludeObjectDefinitions
            ? LoadDefinitionsAsync(ids, ct)
            : Task.FromResult(new Dictionary<int, string?>());
        var colTask = WithConnAsync(ReadViewColumnsAsync, ct);
        await Task.WhenAll(defTask, colTask);

        var defs = await defTask;
        var cols = await colTask;
        for (var i = 0; i < views.Count; i++)
        {
            views[i].Definition = defs.GetValueOrDefault(views[i].ObjectId);
            views[i].Columns = cols.GetValueOrDefault(views[i].ObjectId, []);
            await channel.Writer.WriteAsync(new SchemaObject { Kind = SchemaObjectKind.View, View = views[i] }, ct);
            if ((i + 1) % 25 == 0 || i == views.Count - 1)
            {
                Logger.LogInformation("👁️ 视图 {Done}/{Total}", i + 1, views.Count);
                progress?.Report(new SchemaProgress(SchemaReadStage.ReadingViews, i + 1, views.Count,
                    15 + 10 * (i + 1) / views.Count));
            }
        }
        Logger.LogInformation("👁️ 视图生产者完成,耗时 {Elapsed:F1}s", sw.Elapsed.TotalSeconds);
    }

    private async Task<List<ViewInfo>> ReadViewListAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = """
            SELECT v.object_id, OBJECT_SCHEMA_NAME(v.object_id), v.name, ep.value
            FROM sys.views v
            LEFT JOIN sys.extended_properties ep ON ep.major_id=v.object_id AND ep.minor_id=0 AND ep.name='MS_Description'
            ORDER BY 2,3
            """;
        var list = new List<ViewInfo>();
        await using var cmd = Cmd(conn, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new ViewInfo
            {
                ObjectId = r.GetInt32(0), Schema = r.GetString(1),
                Name = r.GetString(2), Description = r.IsDBNull(3) ? null : r.GetString(3),
            });
        return list;
    }

    private async Task<Dictionary<int, List<ColumnInfo>>> ReadViewColumnsAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = $"""
            SELECT c.object_id, c.name, c.column_id, UPPER(tp.name), {DataTypeExpr("c")}, c.is_nullable, ep.value
            FROM sys.columns c
            INNER JOIN sys.views v ON c.object_id=v.object_id
            INNER JOIN sys.types tp ON c.user_type_id=tp.user_type_id
            LEFT JOIN sys.extended_properties ep ON ep.major_id=c.object_id AND ep.minor_id=c.column_id AND ep.name='MS_Description'
            ORDER BY c.object_id, c.column_id
            -- HASH JOIN 强制各目录表一次扫描,避免逐行嵌套探测的随机 IO(同 Tables 全量字段)
            OPTION (HASH JOIN, FORCE ORDER)
            """;
        var result = new Dictionary<int, List<ColumnInfo>>();
        await using var cmd = Cmd(conn, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var col = new ColumnInfo
            {
                Name = r.GetString(1), Ordinal = r.GetInt32(2),
                DataType = r.GetString(3), DataTypeFull = r.GetString(4),
                IsNullable = r.GetBoolean(5),
                Description = r.IsDBNull(6) ? null : r.GetString(6),
            };
            var id = r.GetInt32(0);
            if (!result.TryGetValue(id, out var l)) result[id] = l = [];
            l.Add(col);
        }
        return result;
    }
}
