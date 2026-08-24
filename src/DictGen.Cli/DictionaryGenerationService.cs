using DictGen.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DictGen.Cli;

/// <summary>
/// 数据字典生成主服务:编排 读取(ISchemaProvider) → 生成(IDataDictionaryGenerator) 全流程。
/// 流式管线:提供器内部 表/视图/过程 三路并发读取 → Channel 背压 → 生成器边收边写。
/// </summary>
public sealed class DictionaryGenerationService(
    IServiceProvider services,
    ILogger<DictionaryGenerationService> logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private IServiceProvider Services { get; } = services;
    private ILogger<DictionaryGenerationService> Logger { get; } = logger;
    private IHostApplicationLifetime Lifetime { get; } = lifetime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var databaseOptions = Services.GetRequiredService<DatabaseOptions>();
            var generationOptions = Services.GetRequiredService<GenerationOptions>();
            var runFlags = Services.GetRequiredService<RunFlags>();

            var providerKey = runFlags.IsSample ? SampleSchemaProvider.ProviderKey : databaseOptions.Provider;
            var provider = Services.GetRequiredKeyedService<ISchemaProvider>(providerKey);
            var generator = Services.GetRequiredKeyedService<IDataDictionaryGenerator>(generationOptions.Generator);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Logger.LogInformation("📡 开始读取数据库结构 (提供器: {ProviderKey})", providerKey);

            // 来源元数据(库名/服务器/版本)用于产物标题与页脚
            SchemaSourceInfo? sourceInfo = null;
            if (provider is ISchemaInfoProvider infoProvider)
                sourceInfo = await infoProvider.GetSourceInfoAsync(stoppingToken);

            var progress = new Progress<SchemaProgress>(p =>
            {
                Logger.LogInformation("[{Stage}] {Done}/{Total} ({Percent}%)", p.Stage.ToString(), p.Completed, p.Total, p.Percent);
            });

            // 流式:边读边生成,Channel 背压,内存有界
            var schemaObjects = provider.EnumerateObjectsAsync(progress, stoppingToken);
            var result = await generator.GenerateAsync(schemaObjects, generationOptions, sourceInfo, stoppingToken);

            Logger.LogInformation("✅ 全部完成: {Objects} 个对象, {Files} 个文件, {Size:F2} MB, 总耗时 {Elapsed}",
                result.ObjectCount, result.FileCount, result.TotalBytes / 1024.0 / 1024.0, sw.Elapsed);
            Logger.LogInformation("📂 输出目录: {Dir}", result.OutputDirectory);
            Logger.LogInformation("👉 双击打开: {Index}", Path.Combine(result.OutputDirectory, "index.html"));

            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ 生成失败: {Message}", ex.Message);
            Environment.ExitCode = 1;
        }
        finally
        {
            Lifetime.StopApplication();
        }
    }
}
