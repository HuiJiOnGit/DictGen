using DictGen.Abstractions.Models;

namespace DictGen.Abstractions;

/// <summary>
/// 数据库结构提供器:从特定数据库引擎读取结构信息。
/// 每种数据库引擎一个实现(如 SQL Server、PostgreSQL),通过接口与生成器解耦。
/// </summary>
public interface ISchemaProvider
{
    /// <summary>一次性读取全部结构(兼容简单场景)。</summary>
    Task<DatabaseSchema> GetSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式读取全部对象:内部按 表/视图/存储过程 并行读取,
    /// 逐对象产出,支持生成器边读边生成,避免全量载入内存。
    /// </summary>
    IAsyncEnumerable<SchemaObject> EnumerateObjectsAsync(
        IProgress<SchemaProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>提供器连接信息(库名/服务器/版本),供生成器在流式模式下使用。</summary>
public interface ISchemaInfoProvider
{
    /// <summary>获取连接目标信息(不读取任何对象)。</summary>
    Task<SchemaSourceInfo> GetSourceInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>数据库来源元数据。</summary>
public sealed class SchemaSourceInfo
{
    public required string DatabaseName { get; init; }
    public string? ServerName { get; init; }
    public string? ServerVersion { get; init; }
}
