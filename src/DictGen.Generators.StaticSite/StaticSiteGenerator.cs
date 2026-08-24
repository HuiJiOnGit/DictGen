using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Channels;
using DictGen.Abstractions;
using DictGen.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace DictGen.Generators.StaticSite;

/// <summary>
/// 静态站点生成器:支持流式(边读边生成)与全量两种模式。
///
/// 流式模式:通过 Channel 多消费者:
/// 生产者将 SchemaObject 送入 ObjectArchive → 分块写满即产出 ChunkJob
/// → 入写盘 Channel → 多个 writer 任务并发写 .js 分块文件。
/// 待全部对象消费完毕:最终写出搜索索引 + 外壳(index.html/app.js/style.css)。
///
/// 数据文件一律以 JS 变量 + 函数调用形式输出(兼容 file:// 双击打开)。
/// </summary>
public sealed class StaticSiteGenerator : IDataDictionaryGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private ILogger<StaticSiteGenerator> Logger { get; }
    private GenerationOptions Options { get; set; } = default!;

    public StaticSiteGenerator() => Logger = null!; // 用于简单场景

    public StaticSiteGenerator(ILogger<StaticSiteGenerator> logger)
    {
        Logger = logger;
    }

    /// <summary>写盘消费者数。</summary>
    private const int WriteConcurrency = 3;

    // ---------- 全量模式(向后兼容) ----------

    public async Task<GenerationResult> GenerateAsync(
        DatabaseSchema schema,
        GenerationOptions options,
        CancellationToken ct = default)
    {
        Options = options;
        PrepareOutputDirectory();
        var archive = new ObjectArchive(schema);
        foreach (var t in schema.Tables) archive.Add(new SchemaObject { Kind = SchemaObjectKind.Table, Table = t });
        foreach (var v in schema.Views) archive.Add(new SchemaObject { Kind = SchemaObjectKind.View, View = v });
        foreach (var p in schema.Procedures) archive.Add(new SchemaObject { Kind = SchemaObjectKind.Procedure, Procedure = p });
        var chunks = archive.Complete().ToList();

        // 写所有分块
        foreach (var job in chunks)
            await WriteChunkFileAsync(job, ct);

        return await WriteAllAsync(archive, chunks, ct);
    }

    // ---------- 流式模式 ----------

    public async Task<GenerationResult> GenerateAsync(
        IAsyncEnumerable<SchemaObject> objects,
        GenerationOptions options,
        SchemaSourceInfo? sourceInfo = null,
        CancellationToken ct = default)
    {
        Options = options;
        PrepareOutputDirectory();

        var archive = new ObjectArchive(sourceInfo);
        Logger?.LogInformation("🚀 流式管线启动: 边读库边生成…");

        // 写盘 Channel: 生产者(归档满块) → 多个 writer 消费者
        var writeChannel = Channel.CreateBounded<ChunkJob>(new BoundedChannelOptions(20)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
        });

        // 启动写盘消费者(并行)
        var writerTasks = Enumerable.Range(0, WriteConcurrency)
            .Select(_ => WriteChunkConsumerAsync(writeChannel.Reader, ct))
            .ToArray();

        // 归档生产者:消费对象流 → 产出 ChunkJob → 入写盘 Channel
        var archiveTask = ArchiveConsumerAsync(objects, archive, writeChannel.Writer, ct);

        // 等归档结束 → 关闭写盘 channel → 等所有 writer 完成
        await archiveTask;
        writeChannel.Writer.TryComplete();
        await Task.WhenAll(writerTasks);

        var chunks = archive.Complete().ToList();

        // 最后几块(未满)写盘
        foreach (var job in chunks)
            await WriteChunkFileAsync(job, ct);

        return await WriteAllAsync(archive, chunks, ct);
    }

    /// <summary>归档消费者:读取对象流 → 归档 → 满块入写盘 channel。</summary>
    private async Task ArchiveConsumerAsync(
        IAsyncEnumerable<SchemaObject> objects,
        ObjectArchive archive,
        ChannelWriter<ChunkJob> writer,
        CancellationToken ct)
    {
        var processed = 0;
        await foreach (var obj in objects.WithCancellation(ct))
        {
            processed++;
            var job = archive.Add(obj);
            if (job is not null)
                await writer.WriteAsync(job, ct);

            if (processed % 200 == 0)
                Logger?.LogInformation("📦 已归档 {Count} 个对象", processed);
        }
        Logger?.LogInformation("📦 归档完成: 共 {Count} 个对象", processed);
    }

    /// <summary>写盘消费者。</summary>
    private async Task WriteChunkConsumerAsync(
        ChannelReader<ChunkJob> reader, CancellationToken ct)
    {
        await foreach (var job in reader.ReadAllAsync(ct))
            await WriteChunkFileAsync(job, ct);
    }

    // ---------- 产出写盘 ----------

    /// <summary>生成前准备输出目录:仅清空 data/ 子目录(外壳文件由前端构建产出,保留)。</summary>
    private void PrepareOutputDirectory()
    {
        var outputDir = Path.GetFullPath(Options.OutputDirectory);
        var dataDir = Path.Combine(outputDir, "data");
        // 保留前端构建的 index.html/app.js/style.css,只重建数据目录
        if (Options.CleanOutputDirectory && Directory.Exists(dataDir))
            Directory.Delete(dataDir, recursive: true);
        Directory.CreateDirectory(outputDir);
        if (!Options.EmbedSingleFile)
            Directory.CreateDirectory(dataDir);
    }

    private async Task<GenerationResult> WriteAllAsync(
        ObjectArchive archive, IEnumerable<ChunkJob> chunks, CancellationToken ct)
    {
        var outputDir = Path.GetFullPath(Options.OutputDirectory);
        var dataDir = Path.Combine(outputDir, "data");

        // 标题
        if (string.IsNullOrWhiteSpace(Options.SiteTitle) || Options.SiteTitle == "数据库数据字典")
        {
            var db = archive.DatabaseName;
            if (!string.IsNullOrWhiteSpace(db)) Options.SiteTitle = $"{db}数据字典";
        }

        // 搜索索引 + meta
        var indexData = JsonSerializer.SerializeToUtf8Bytes(
            archive.BuildSearchIndex(Options.SiteTitle, DateTime.Now), JsonOptions);
        var indexScript = new byte[indexData.Length + "window.__DICT=".Length + 2];
        Encoding.UTF8.GetBytes("window.__DICT=", 0, "window.__DICT=".Length, indexScript, 0);
        indexData.CopyTo(indexScript, "window.__DICT=".Length);
        indexScript[^2] = (byte)';'; indexScript[^1] = (byte)'\n';

        long totalBytes = 0;
        var fileCount = 0;

        // 记录分块文件(已在流式/全量阶段写入)
        var writtenChunks = chunks.Where(c => File.Exists(Path.Combine(dataDir, c.ChunkId + ".js"))).ToList();
        fileCount += writtenChunks.Count;
        totalBytes += writtenChunks.Sum(c => new FileInfo(Path.Combine(dataDir, c.ChunkId + ".js")).Length);

        if (Options.EmbedSingleFile)
        {
            // 单文件:全部内联。外壳文件由前端输出,不存在则跳过。
            var htmlPath = Path.Combine(outputDir, "index.html");
            var cssPath = Path.Combine(outputDir, "style.css");
            var jsPath = Path.Combine(outputDir, "app.js");
            if (File.Exists(htmlPath))
            {
                var html = (await File.ReadAllTextAsync(htmlPath, ct))
                    .Replace("{{TITLE}}", Options.SiteTitle)
                    .Replace("<link rel=\"stylesheet\" href=\"./style.css\">", "")
                    .Replace("<script src=\"./app.js\"></script>", "");
                var sb = new StringBuilder(html.Length + indexScript.Length + 256 * 1024);
                sb.Append(html);
                if (File.Exists(cssPath))
                    sb.Append("<style>").Append(await File.ReadAllTextAsync(cssPath, ct)).Append("</style>\n");
                sb.Append("<script>").Append(Encoding.UTF8.GetString(indexScript)).Append("</script>\n");
                if (File.Exists(jsPath))
                    sb.Append("<script>").Append(Encoding.UTF8.GetString(await File.ReadAllBytesAsync(jsPath, ct))).Append("</script>\n");
                foreach (var c in chunks)
                {
                    var chunkData = JsonSerializer.SerializeToUtf8Bytes(c.Objects, JsonOptions);
                    var script = $"window.__DICT_CHUNK(\"{c.ChunkId}\"," + Encoding.UTF8.GetString(chunkData) + ");\n";
                    sb.Append("<script>").Append(script).Append("</script>\n");
                }

                var output = Encoding.UTF8.GetBytes(sb.ToString());
                await File.WriteAllBytesAsync(Path.Combine(outputDir, "index.html"), output, ct);
                totalBytes = output.Length;
                fileCount = 1;
            }
        }
        else
        {
            // 分块模式:数据文件由后端写入,外壳文件(index.html/app.js/style.css)
            // 由前端 pnpm build 产出。外壳不存在时只写数据,不报错也不提示。
            await File.WriteAllBytesAsync(Path.Combine(dataDir, "search-index.js"), indexScript, ct);
            totalBytes += indexScript.Length; fileCount++;

            var ver = DateTime.Now.ToString("yyyyMMddHHmmss");
            var chunkScriptTags = string.Join("\n",
                chunks.Select(c => $"  <script src=\"data/{c.ChunkId}.js\"></script>"));

            var htmlPath = Path.Combine(outputDir, "index.html");
            if (File.Exists(htmlPath))
            {
                var html = await File.ReadAllTextAsync(htmlPath, ct);

                // 注入分块脚本(幂等:已注入则跳过)
                if (html.Contains("<!--DICT_CHUNKS-->"))
                    html = html.Replace("<!--DICT_CHUNKS-->", chunkScriptTags);

                // 版本号参数避免浏览器缓存旧前端
                html = html
                    .Replace("{{TITLE}}", Options.SiteTitle)
                    .Replace("<script src=\"./app.js\"></script>",
                        "<script src=\"./app.js?v=" + ver + "\"></script>")
                    .Replace("href=\"./style.css\"", "href=\"./style.css?v=" + ver + "\"");
                await File.WriteAllTextAsync(Path.Combine(outputDir, "index.html"), html,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
                totalBytes += Encoding.UTF8.GetByteCount(html);
                fileCount++;
            }
        }

        Logger?.LogInformation("🎉 生成完成: {Files} 个文件, {Size:F2} MB, 输出 → {Dir}",
            fileCount, totalBytes / 1024.0 / 1024.0, outputDir);

        return new GenerationResult
        {
            OutputDirectory = outputDir, FileCount = fileCount,
            TotalBytes = totalBytes, ObjectCount = archive.ObjectCount,
        };
    }

    /// <summary>写入一个分块文件。</summary>
    private async Task WriteChunkFileAsync(ChunkJob job, CancellationToken ct)
    {
        var outputDir = Path.GetFullPath(Options.OutputDirectory);
        var dataDir = Path.Combine(outputDir, "data");
        Directory.CreateDirectory(dataDir);

        var chunkData = JsonSerializer.SerializeToUtf8Bytes(job.Objects, JsonOptions);
        var script = Encoding.UTF8.GetBytes(
            $"window.__DICT_CHUNK(\"{job.ChunkId}\"," + Encoding.UTF8.GetString(chunkData) + ");\n");
        await File.WriteAllBytesAsync(Path.Combine(dataDir, job.ChunkId + ".js"), script, ct);
    }
}
