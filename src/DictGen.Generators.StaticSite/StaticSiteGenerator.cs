using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Channels;
using DictGen.Abstractions;
using DictGen.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace DictGen.Generators.StaticSite;

/// <summary>
/// 静态站点生成器:流式管线,通过 Channel 多消费者:
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

    public StaticSiteGenerator(ILogger<StaticSiteGenerator> logger)
    {
        Logger = logger;
    }

    /// <summary>写盘消费者数。</summary>
    private const int WriteConcurrency = 3;

    /// <summary>原始外壳(vite 构建产物)的判别特征:分块占位符与外部 app.js 引用并存;被生成器处理过之后至少其一消失。</summary>
    private static bool IsPristineShell(string html) =>
        html.Contains("<!--DICT_CHUNKS-->") && html.Contains("<script src=\"./app.js\"></script>");

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

        // 等归档结束 → 关闭写盘 channel(生产者异常也经此传给消费者)→ 等所有 writer 完成
        try
        {
            await archiveTask;
        }
        finally
        {
            writeChannel.Writer.TryComplete();
        }
        await Task.WhenAll(writerTasks);

        // 收尾:仅写盘未满的尾块。满块已由 writer 写过,不再重复序列化与覆盖写。
        foreach (var job in archive.Complete())
            await WriteChunkFileAsync(job, ct);

        return await WriteAllAsync(archive, ct);
    }

    /// <summary>归档消费者:读取对象流 → 归档 → 满块入写盘 channel。</summary>
    private async Task ArchiveConsumerAsync(
        IAsyncEnumerable<SchemaObject> objects,
        ObjectArchive archive,
        ChannelWriter<ChunkJob> writer,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
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
        Logger?.LogInformation("📦 归档完成: 共 {Count} 个对象, 耗时 {Elapsed:F1}s", processed, sw.Elapsed.TotalSeconds);
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
        // 保留前端构建的 index.html/app.js/style.css,只重建数据目录;
        // data 目录无条件创建:单文件模式下分块也会先落盘再被内联读取
        if (Options.CleanOutputDirectory && Directory.Exists(dataDir))
            Directory.Delete(dataDir, recursive: true);
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(dataDir);
    }

    private async Task<GenerationResult> WriteAllAsync(ObjectArchive archive, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var outputDir = Path.GetFullPath(Options.OutputDirectory);
        var dataDir = Path.Combine(outputDir, "data");

        // 标题缺省时回填库名。用局部变量,不回写 DI 共享的 Options 实例。
        var siteTitle = Options.SiteTitle;
        if (string.IsNullOrWhiteSpace(siteTitle) || siteTitle == "数据库数据字典")
        {
            var db = archive.DatabaseName;
            if (!string.IsNullOrWhiteSpace(db)) siteTitle = $"{db}数据字典";
        }

        // 搜索索引 + meta
        var indexData = JsonSerializer.SerializeToUtf8Bytes(
            archive.BuildSearchIndex(siteTitle, DateTime.Now), JsonOptions);
        var indexScript = new byte[indexData.Length + "window.__DICT=".Length + 2];
        Encoding.UTF8.GetBytes("window.__DICT=", 0, "window.__DICT=".Length, indexScript, 0);
        indexData.CopyTo(indexScript, "window.__DICT=".Length);
        indexScript[^2] = (byte)';'; indexScript[^1] = (byte)'\n';

        var (totalBytes, fileCount) = Options.EmbedSingleFile
            ? await WriteEmbeddedAsync(archive, outputDir, dataDir, indexScript, ct)
            : await WriteChunkedShellAsync(archive, outputDir, dataDir, indexScript, ct);

        Logger?.LogInformation("🎉 生成完成: {Files} 个文件, {Size:F2} MB, 收尾耗时 {Elapsed:F1}s, 输出 → {Dir}",
            fileCount, totalBytes / 1024.0 / 1024.0, sw.Elapsed.TotalSeconds, outputDir);

        return new GenerationResult
        {
            OutputDirectory = outputDir, FileCount = fileCount,
            TotalBytes = totalBytes, ObjectCount = archive.ObjectCount,
        };
    }

    /// <summary>
    /// 读取原始前端外壳。生成器会覆写 output/index.html(注入分块标签或内联单文件),
    /// 故首次处理前把原始外壳留档为 index.shell.html,之后始终从留档读取;
    /// index.html 内容为原始外壳(即前端重新构建过)时刷新留档。
    /// </summary>
    private async Task<string?> ReadShellAsync(string outputDir, CancellationToken ct)
    {
        var htmlPath = Path.Combine(outputDir, "index.html");
        var shellPath = Path.Combine(outputDir, "index.shell.html");
        if (!File.Exists(htmlPath)) return null;

        // index.html 每次生成都被覆写(时间戳总比留档新),不能只看时间戳;
        // 只有内容为原始外壳时才刷新留档,否则视为生成器自己的输出而沿用旧留档。
        if (IsPristineShell(await File.ReadAllTextAsync(htmlPath, ct)) &&
            (!File.Exists(shellPath) ||
             File.GetLastWriteTimeUtc(htmlPath) > File.GetLastWriteTimeUtc(shellPath)))
            File.Copy(htmlPath, shellPath, overwrite: true);

        if (!File.Exists(shellPath))
        {
            Logger?.LogWarning(
                "⚠️ 未找到可用的原始前端外壳({Html} 已被注入或内联覆盖且无留档),本次仅写出数据文件。请重新执行前端构建后再生成。",
                htmlPath);
            return null;
        }
        return await File.ReadAllTextAsync(shellPath, ct);
    }

    /// <summary>
    /// 单文件模式:把 css/app.js 与全部分块内联进 index.html。
    /// 分块直接复用已写盘的字节流(不再二次序列化),顺序写出,全程无 string 往返。
    /// </summary>
    private async Task<(long TotalBytes, int FileCount)> WriteEmbeddedAsync(
        ObjectArchive archive, string outputDir, string dataDir,
        byte[] indexScript, CancellationToken ct)
    {
        var shell = await ReadShellAsync(outputDir, ct);
        if (shell is null) return (0, 0);

        // 站点标题经搜索索引 meta 由前端动态应用到 <title>,模板本身不含 {{TITLE}}
        var html = shell
            .Replace("<link rel=\"stylesheet\" href=\"./style.css\">", "")
            .Replace("<script src=\"./app.js\"></script>", "");

        await using var output = File.Create(Path.Combine(outputDir, "index.html"));
        long totalBytes = 0;

        async Task WriteTextAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await output.WriteAsync(bytes, ct);
            totalBytes += bytes.Length;
        }

        async Task EmbedFileAsync(string? path)
        {
            if (!File.Exists(path)) return;
            await using var src = File.OpenRead(path);
            totalBytes += src.Length;
            await src.CopyToAsync(output, ct);
        }

        await WriteTextAsync(html);
        if (File.Exists(Path.Combine(outputDir, "style.css")))
        {
            await WriteTextAsync("<style>");
            await EmbedFileAsync(Path.Combine(outputDir, "style.css"));
            await WriteTextAsync("</style>\n");
        }
        await WriteTextAsync("<script>");
        await output.WriteAsync(indexScript, ct);
        totalBytes += indexScript.Length;
        await WriteTextAsync("</script>\n");
        await WriteTextAsync("<script>");
        await EmbedFileAsync(Path.Combine(outputDir, "app.js"));
        await WriteTextAsync("</script>\n");
        foreach (var chunkId in archive.ChunkIds)
        {
            await WriteTextAsync("<script>");
            await EmbedFileAsync(Path.Combine(dataDir, chunkId + ".js"));
            await WriteTextAsync("</script>\n");
        }

        return (totalBytes, 1);
    }

    /// <summary>
    /// 分块模式:写搜索索引;把分块脚本清单注入前端构建产出的外壳 index.html
    /// (外壳不存在时只写数据,不报错也不提示)。
    /// </summary>
    private async Task<(long TotalBytes, int FileCount)> WriteChunkedShellAsync(
        ObjectArchive archive, string outputDir, string dataDir,
        byte[] indexScript, CancellationToken ct)
    {
        // 分块文件已由写盘管线/收尾写出,这里只按实际落盘文件统计
        long totalBytes = 0;
        var fileCount = 0;
        foreach (var f in Directory.EnumerateFiles(dataDir, "*.js"))
        {
            if (Path.GetFileName(f) == "search-index.js") continue; // 下面重写后按新长度计入
            totalBytes += new FileInfo(f).Length;
            fileCount++;
        }

        await File.WriteAllBytesAsync(Path.Combine(dataDir, "search-index.js"), indexScript, ct);
        totalBytes += indexScript.Length;
        fileCount++;

        var ver = DateTime.Now.ToString("yyyyMMddHHmmss");
        var chunkScriptTags = string.Join("\n",
            archive.ChunkIds.Select(id => $"  <script src=\"data/{id}.js\"></script>"));

        // 外壳不存在时只写数据,不报错也不提示
        var shell = await ReadShellAsync(outputDir, ct);
        if (shell is not null)
        {
            var html = InjectChunkScripts(shell, chunkScriptTags);
            if (html is null)
            {
                Logger?.LogWarning(
                    "⚠️ 外壳缺少 <!--DICT_CHUNKS--> 占位符(非原始前端构建产物),已跳过 index.html 注入。请重新执行前端构建后再生成。");
            }
            else
            {
                // 版本号参数避免浏览器缓存旧前端
                html = Regex.Replace(html, "<script src=\"\\./app\\.js\"></script>",
                    _ => "<script src=\"./app.js?v=" + ver + "\"></script>");
                html = Regex.Replace(html, "href=\"\\./style\\.css\"",
                    _ => "href=\"./style.css?v=" + ver + "\"");

                await File.WriteAllTextAsync(Path.Combine(outputDir, "index.html"), html,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
                totalBytes += Encoding.UTF8.GetByteCount(html);
                fileCount++;
            }
        }
        return (totalBytes, fileCount);
    }

    /// <summary>
    /// 把分块脚本清单注入外壳 html,幂等可重复生成:
    /// 首次替换 <c>&lt;!--DICT_CHUNKS--&gt;</c> 占位符并保留边界标记;
    /// 之后整体替换标记区间。外壳无占位符时返回 null(由调用方告警)。
    /// </summary>
    private static string? InjectChunkScripts(string html, string chunkScriptTags)
    {
        var block = $"<!--DICT_CHUNKS-->\n{chunkScriptTags}\n<!--/DICT_CHUNKS-->";
        if (html.Contains("<!--/DICT_CHUNKS-->"))
            return Regex.Replace(html, "<!--DICT_CHUNKS-->[\\s\\S]*?<!--/DICT_CHUNKS-->", _ => block);
        if (html.Contains("<!--DICT_CHUNKS-->"))
            return html.Replace("<!--DICT_CHUNKS-->", block);
        return null;
    }

    /// <summary>写入一个分块文件(data 目录由 <see cref="PrepareOutputDirectory"/> 统一创建)。</summary>
    private async Task WriteChunkFileAsync(ChunkJob job, CancellationToken ct)
    {
        var outputDir = Path.GetFullPath(Options.OutputDirectory);
        var dataDir = Path.Combine(outputDir, "data");

        var chunkData = JsonSerializer.SerializeToUtf8Bytes(job.Objects, JsonOptions);
        await File.WriteAllBytesAsync(Path.Combine(dataDir, job.ChunkId + ".js"),
            BuildChunkScript(job.ChunkId, chunkData), ct);
    }

    /// <summary>分块的 JS 包装:<c>window.__DICT_CHUNK("id",{json});</c>,字节级拼接避免 UTF8↔string 往返。</summary>
    private static byte[] BuildChunkScript(string chunkId, byte[] json)
    {
        var head = Encoding.UTF8.GetBytes($"window.__DICT_CHUNK(\"{chunkId}\",");
        var tail = Encoding.UTF8.GetBytes(");\n");
        var script = new byte[head.Length + json.Length + tail.Length];
        Buffer.BlockCopy(head, 0, script, 0, head.Length);
        Buffer.BlockCopy(json, 0, script, head.Length, json.Length);
        Buffer.BlockCopy(tail, 0, script, head.Length + json.Length, tail.Length);
        return script;
    }
}
