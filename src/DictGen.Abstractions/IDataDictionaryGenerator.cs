using DictGen.Abstractions.Models;

namespace DictGen.Abstractions;

/// <summary>
/// 数据字典产物生成器:把 <see cref="DatabaseSchema"/> 渲染为某种形式的产物(如静态站点)。
/// 每种产物形式一个实现,通过接口与提供器解耦。
/// </summary>
public interface IDataDictionaryGenerator
{
    /// <summary>基于完整结构生成(简单场景)。</summary>
    Task<GenerationResult> GenerateAsync(
        DatabaseSchema schema,
        GenerationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式生成:逐对象消费 <see cref="SchemaObject"/> 流,
    /// 边接收边分块写出,支持多消费者并行写盘,内存占用有界。
    /// </summary>
    /// <param name="sourceInfo">可选的来源元数据(库名/服务器/版本),用于产物标题与页脚。</param>
    Task<GenerationResult> GenerateAsync(
        IAsyncEnumerable<SchemaObject> objects,
        GenerationOptions options,
        SchemaSourceInfo? sourceInfo = null,
        CancellationToken cancellationToken = default);
}

/// <summary>生成结果统计。</summary>
public sealed class GenerationResult
{
    public required string OutputDirectory { get; init; }

    public required int FileCount { get; init; }

    public required long TotalBytes { get; init; }

    public required int ObjectCount { get; init; }
}
