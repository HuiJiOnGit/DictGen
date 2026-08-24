using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using DictGen.Abstractions;
using System.Threading.Channels;
using DictGen.Abstractions.Models;
using Microsoft.Data.SqlClient;

namespace DictGen.EFCore.SqlServer;

/// <summary>
/// SqlServerSchemaProvider 的存储过程相关 partial。
/// </summary>
internal sealed partial class SqlServerSchemaProvider
{
    // ======== 存储过程生产者(流式) ========

    private async Task ProduceProceduresAsync(
        Channel<SchemaObject> channel, IProgress<SchemaProgress>? progress, CancellationToken ct)
    {
        var procs = await WithConnAsync(ReadProcListAsync, ct);
        if (procs.Count == 0) return;
        Logger.LogInformation("⚙️ 过程清单: {Count} 个", procs.Count);

        var ids = procs.Select(p => p.ObjectId).ToList();
        var defTask = Options.IncludeObjectDefinitions
            ? WithConnAsync((c, t) => LoadDefinitionsAsync(c, ids, t), ct)
            : Task.FromResult(new Dictionary<int, string?>());
        var paramTask = WithConnAsync(ReadProcParamsAsync, ct);
        await Task.WhenAll(defTask, paramTask);

        var defs = await defTask;
        var prms = await paramTask;
        for (var i = 0; i < procs.Count; i++)
        {
            procs[i].Definition = defs.GetValueOrDefault(procs[i].ObjectId);
            procs[i].Parameters = prms.GetValueOrDefault(procs[i].ObjectId, []);
            await channel.Writer.WriteAsync(new SchemaObject { Kind = SchemaObjectKind.Procedure, Procedure = procs[i] }, ct);
            if ((i + 1) % 25 == 0 || i == procs.Count - 1)
            {
                Logger.LogInformation("⚙️ 过程 {Done}/{Total}", i + 1, procs.Count);
                progress?.Report(new SchemaProgress(SchemaReadStage.ReadingProcedures, i + 1, procs.Count, 15 + 10));
            }
        }
    }

    private async Task<List<ProcedureInfo>> ReadProcListAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = """
            SELECT p.object_id, OBJECT_SCHEMA_NAME(p.object_id), p.name, ep.value
            FROM sys.procedures p
            LEFT JOIN sys.extended_properties ep ON ep.major_id=p.object_id AND ep.minor_id=0 AND ep.name='MS_Description'
            WHERE p.is_ms_shipped=0
            ORDER BY 2,3
            """;
        var list = new List<ProcedureInfo>();
        await using var cmd = Cmd(conn, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new ProcedureInfo
            {
                ObjectId = r.GetInt32(0), Schema = r.GetString(1),
                Name = r.GetString(2), Description = r.IsDBNull(3) ? null : r.GetString(3),
            });
        return list;
    }

    private async Task<Dictionary<int, List<ParameterInfo>>> ReadProcParamsAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = """
            SELECT p.object_id, p.name, p.parameter_id,
                UPPER(tp.name)+CASE WHEN tp.name IN ('nvarchar','nchar') THEN '('+CAST(p.max_length/2 AS NVARCHAR)+')'
                    WHEN tp.name IN ('varchar','char','varbinary','binary') AND p.max_length=-1 THEN '(max)'
                    WHEN tp.name IN ('varchar','char','varbinary','binary') THEN '('+CAST(p.max_length AS NVARCHAR)+')'
                    WHEN tp.name IN ('decimal','numeric') THEN '('+CAST(p.[precision] AS NVARCHAR)+','+CAST(p.scale AS NVARCHAR)+')'
                    ELSE '' END,
                CASE WHEN p.is_output=1 THEN 'OUT' ELSE 'IN' END, dc.definition
            FROM sys.parameters p
            INNER JOIN sys.procedures pr ON p.object_id=pr.object_id
            INNER JOIN sys.types tp ON p.user_type_id=tp.user_type_id
            LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=p.object_id AND dc.parent_column_id=p.parameter_id
            WHERE p.parameter_id>0 AND pr.is_ms_shipped=0
            ORDER BY p.object_id, p.parameter_id
            """;
        var result = new Dictionary<int, List<ParameterInfo>>();
        await using var cmd = Cmd(conn, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var param = new ParameterInfo
            {
                Name = r.GetString(1), Ordinal = r.GetInt32(2),
                DataTypeFull = r.GetString(3), Direction = r.GetString(4),
                DefaultValue = r.IsDBNull(5) ? null : r.GetString(5),
            };
            var id = r.GetInt32(0);
            if (!result.TryGetValue(id, out var l)) result[id] = l = [];
            l.Add(param);
        }
        return result;
    }

    // ========== 非流式向后兼容 ==========

    private async Task<List<ProcedureInfo>> FetchProceduresAsync(SqlConnection conn, CancellationToken ct)
    {
        var procs = await ReadProcListAsync(conn, ct);
        if (procs.Count == 0) return procs;
        var ids = procs.Select(p => p.ObjectId).ToList();
        var defTask = Options.IncludeObjectDefinitions ? LoadDefinitionsAsync(conn, ids, ct) : Task.FromResult(new Dictionary<int, string?>());
        var paramTask = ReadProcParamsAsync(conn, ct);
        await Task.WhenAll(defTask, paramTask);
        var defs = await defTask; var prms = await paramTask;
        foreach (var p in procs) { p.Definition = defs.GetValueOrDefault(p.ObjectId); p.Parameters = prms.GetValueOrDefault(p.ObjectId, []); }
        return procs;
    }
}
