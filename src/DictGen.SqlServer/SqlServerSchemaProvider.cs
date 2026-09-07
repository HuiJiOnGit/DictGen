using System.IO.Compression;
using System.Text;
using System.Threading.Channels;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DictGen.Abstractions;
using DictGen.Abstractions.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace DictGen.SqlServer;

/// <summary>
/// SQL Server 数据库结构提供器。
/// 全库元数据分三大路并行读取,通过 <see cref="Channel{T}"/> 逐对象产出,
/// 支持生成器边读边生成,内存有界。
/// </summary>
internal sealed partial class SqlServerSchemaProvider : ISchemaProvider, ISchemaInfoProvider
{
    // object_id 为 int,IN 列表开销极小;500 一批把大库的串行往返从几十次压到个位数
    private const int DefinitionBatchSize = 500;

    // 批与批之间无依赖,有界并发把远程库的批次往返从串行压成并发组
    private const int DefinitionBatchConcurrency = 4;
    private const int CommandTimeoutSeconds = 300;

    private GenerationOptions Options { get; }
    private ILogger<SqlServerSchemaProvider> Logger { get; }
    private string ConnectionString { get; }

    public SqlServerSchemaProvider(
        DatabaseOptions databaseOptions,
        GenerationOptions options,
        ILogger<SqlServerSchemaProvider> logger)
    {
        ConnectionString = databaseOptions.ConnectionString;
        Options = options;
        Logger = logger;
    }

    private SqlCommand Cmd(SqlConnection conn, string sql) =>
        new(sql, conn) { CommandTimeout = CommandTimeoutSeconds };

    /// <summary>打开一个新连接执行 action(ADO.NET 连接池兜底复用)。</summary>
    private async Task<T> WithConnAsync<T>(Func<SqlConnection, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(ct);
        return await action(conn, ct);
    }

    // ========== 连接信息 ==========

    public async Task<SchemaSourceInfo> GetSourceInfoAsync(CancellationToken ct = default)
    {
        return await WithConnAsync(async (conn, t) =>
        {
            var (server, ver) = await GetServerInfoAsync(conn, t);
            return new SchemaSourceInfo { DatabaseName = conn.Database, ServerName = server, ServerVersion = ver };
        }, ct);
    }

    // ========== 入口 ==========

    public async IAsyncEnumerable<SchemaObject> EnumerateObjectsAsync(
        IProgress<SchemaProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<SchemaObject>(new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
        });

        var tasks = new List<Task>();
        if (Options.IncludeTables) tasks.Add(ProduceTablesAsync(channel, progress, ct));
        if (Options.IncludeViews) tasks.Add(ProduceViewsAsync(channel, progress, ct));
        if (Options.IncludeProcedures) tasks.Add(ProduceProceduresAsync(channel, progress, ct));

        // 生产者异常必须经 channel 传给消费者:静默完成会产出残缺字典且退出码为 0
        _ = Task.WhenAll(tasks).ContinueWith(t =>
        {
            if (t.IsFaulted)
                Logger.LogError(t.Exception, "读库生产者异常,提前结束对象流");
            channel.Writer.TryComplete(t.IsFaulted ? t.Exception : null);
        }, ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        await foreach (var obj in channel.Reader.ReadAllAsync(ct))
            yield return obj;
    }

    // ========== 通用辅助 ==========

    private static async Task<(string, string?)> GetServerInfoAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            "SELECT SERVERPROPERTY('MachineName'), SERVERPROPERTY('ProductVersion'), DB_NAME()", conn)
        { CommandTimeout = 30 };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (await r.ReadAsync(ct))
            return (r.GetString(0), r.IsDBNull(1) ? null : "SQL Server " + r.GetString(1));
        return ("(Unknown)", null);
    }

    /// <summary>解压 COMPRESS 的 GZIP 载荷。</summary>
    private static byte[] DecompressGzip(byte[] compressed)
    {
        using var src = new MemoryStream(compressed);
        using var gz = new GZipStream(src, CompressionMode.Decompress);
        using var dst = new MemoryStream(compressed.Length * 2);
        gz.CopyTo(dst);
        return dst.ToArray();
    }

    /// <summary>
    /// 并行加载定义文本:按 <see cref="DefinitionBatchSize"/> 切批(索引切片,避免 Skip 的 O(n²)),
    /// 每批独立连接、按 <see cref="DefinitionBatchConcurrency"/> 有界并发,结果按 object_id 合并
    /// (各批键互不相交)。经 COMPRESS 以 GZIP 传输、客户端解压(文本逐字节一致),
    /// 实测远程库瓶颈为聚合带宽而非往返次数,压缩是唯一有效的提速手段。
    /// </summary>
    private async Task<Dictionary<int, string?>> LoadDefinitionsAsync(
        IReadOnlyList<int> ids, CancellationToken ct)
    {
        var defs = new Dictionary<int, string?>(ids.Count);
        if (ids.Count == 0) return defs;

        var batches = new List<List<int>>();
        for (var i = 0; i < ids.Count; i += DefinitionBatchSize)
        {
            var take = Math.Min(DefinitionBatchSize, ids.Count - i);
            var batch = new List<int>(take);
            for (var j = i; j < i + take; j++) batch.Add(ids[j]);
            batches.Add(batch);
        }

        var sw = Stopwatch.StartNew();
        long transferred = 0;
        using var gate = new SemaphoreSlim(DefinitionBatchConcurrency);
        var batchIndex = 0;
        var batchDefs = await Task.WhenAll(batches.Select(async batch =>
        {
            var bsw = Stopwatch.StartNew();
            await gate.WaitAsync(ct);
            try
            {
                await using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync(ct);
                var result = new Dictionary<int, string?>(batch.Count);
                var idsSql = string.Join(",", batch);
                await using var cmd = Cmd(conn,
                    $"SELECT object_id, COMPRESS(definition) FROM sys.sql_modules WHERE object_id IN ({idsSql})");
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var payload = r.IsDBNull(1) ? null : (byte[])r.GetValue(1);
                    Interlocked.Add(ref transferred, payload?.Length ?? 0);
                    result[r.GetInt32(0)] = payload is null
                        ? null
                        : Encoding.Unicode.GetString(DecompressGzip(payload));
                }

                Logger.LogInformation("📄 定义批次 {Index}/{Total}({Count} 项)耗时 {Elapsed:F1}s",
                    Interlocked.Increment(ref batchIndex), batches.Count, batch.Count, bsw.Elapsed.TotalSeconds);
                return result;
            }
            finally
            {
                gate.Release();
            }
        }));
        foreach (var result in batchDefs)
            foreach (var kv in result)
                defs[kv.Key] = kv.Value;

        Logger.LogInformation("📄 定义文本 {Count} 项 / {Batches} 批(并发 {Concurrency}, gzip)耗时 {Elapsed:F1}s, 传输约 {Mb:F1} MB",
            ids.Count, batches.Count, DefinitionBatchConcurrency, sw.Elapsed.TotalSeconds,
            transferred / 1024.0 / 1024.0);
        return defs;
    }

    /// <summary>字段类型显示字符串,统一生成的 CASE 逻辑。</summary>
    private static string DataTypeExpr(string col) => $@"
        CASE WHEN tp.name IN ('nvarchar','nchar','varbinary','varchar','char','binary') AND {col}.max_length = -1 THEN UPPER(tp.name)+'(max)'
             WHEN tp.name IN ('nvarchar','nchar') THEN UPPER(tp.name)+'('+CAST({col}.max_length/2 AS NVARCHAR)+')'
             WHEN tp.name IN ('varchar','char','varbinary','binary') THEN UPPER(tp.name)+'('+CAST({col}.max_length AS NVARCHAR)+')'
             WHEN tp.name IN ('decimal','numeric') THEN UPPER(tp.name)+'('+CAST({col}.[precision] AS NVARCHAR)+','+CAST({col}.scale AS NVARCHAR)+')'
             WHEN tp.name IN ('datetime2','datetimeoffset','time') THEN UPPER(tp.name)+'('+CAST({col}.scale AS NVARCHAR)+')'
             ELSE UPPER(tp.name) END";
}
