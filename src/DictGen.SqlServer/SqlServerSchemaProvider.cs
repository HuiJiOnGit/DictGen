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

    public async Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(ct);
        var (server, ver) = await GetServerInfoAsync(conn, ct);
        var db = conn.Database;

        var tTask = Options.IncludeTables ? FetchTablesAsync(conn, ct) : Task.FromResult(new List<TableInfo>());
        var vTask = Options.IncludeViews ? FetchViewsAsync(conn, ct) : Task.FromResult(new List<ViewInfo>());
        var pTask = Options.IncludeProcedures ? FetchProceduresAsync(conn, ct) : Task.FromResult(new List<ProcedureInfo>());
        await Task.WhenAll(tTask, vTask, pTask);

        Logger.LogInformation("🏁 结构读取完成: {Tables} 表, {Views} 视图, {Procs} 过程, 耗时 {Elapsed}",
            (await tTask).Count, (await vTask).Count, (await pTask).Count, sw.Elapsed);
        return new DatabaseSchema
        {
            DatabaseName = db, ServerName = server, ServerVersion = ver,
            Tables = await tTask, Views = await vTask, Procedures = await pTask,
        };
    }

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

    private async Task<Dictionary<int, string?>> LoadDefinitionsAsync(
        SqlConnection conn, IReadOnlyList<int> ids, CancellationToken ct)
    {
        var defs = new Dictionary<int, string?>();
        for (var i = 0; i < ids.Count; i += DefinitionBatchSize)
        {
            var batch = ids.Skip(i).Take(DefinitionBatchSize).ToList();
            var idsSql = string.Join(",", batch);
            await using var cmd = Cmd(conn,
                $"SELECT object_id, definition FROM sys.sql_modules WHERE object_id IN ({idsSql})");
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                defs[r.GetInt32(0)] = r.IsDBNull(1) ? null : r.GetString(1);
        }
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
