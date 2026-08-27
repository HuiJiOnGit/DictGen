using DictGen.Abstractions;
using DictGen.Cli;
using DictGen.SqlServer;
using DictGen.Generators.StaticSite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ---------- Host 构建 ----------
var builder = Host.CreateApplicationBuilder(args);

// 控制台应用的工作目录不一定是程序目录,显式加载程序目录下的 appsettings.json。
// 注意:必须显式指定 FileProvider —— 裸 JsonConfigurationSource 会退回到内容根目录的
// 文件提供器,无法解析绝对路径,Optional=true 时会静默跳过导致整个文件不生效。
// 插入到最前面保证环境变量 / 命令行参数仍可覆盖文件配置。
builder.Configuration.Sources.Insert(0, new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
{
    Path = "appsettings.json",
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory),
    Optional = true,
    ReloadOnChange = false,
});

var databaseOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
var generationOptions = builder.Configuration.GetSection("Generation").Get<GenerationOptions>() ?? new GenerationOptions();
var isSample = args.Contains("--sample");

// 输出目录锚定到解决方案根(含 DictGen.slnx 的目录):
// 无论从哪个工作目录启动(dotnet run / 直接运行 exe / bin 目录),
// 相对路径 OutputDirectory 都解析到同一位置,与前端 pnpm build 的产物一致。
var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
if (!Path.IsPathRooted(generationOptions.OutputDirectory))
    generationOptions.OutputDirectory = Path.Combine(solutionRoot, generationOptions.OutputDirectory);

builder.Services.AddSingleton(databaseOptions);
builder.Services.AddSingleton(generationOptions);
builder.Services.AddSingleton(new RunFlags { IsSample = isSample });

builder.Services.AddStaticSiteGenerator();

if (isSample)
{
    builder.Services.AddKeyedSingleton<ISchemaProvider, SampleSchemaProvider>(SampleSchemaProvider.ProviderKey);
}
else
{
    if (string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
    {
        Console.Error.WriteLine("未配置连接串: 请编辑 appsettings.json 的 Database:ConnectionString,或使用 --sample 运行示例。");
        return 2;
    }
    builder.Services.AddSqlServerProvider();
}

builder.Services.AddHostedService<DictionaryGenerationService>();

using var host = builder.Build();
await host.RunAsync();
return Environment.ExitCode;

/// <summary>从给定目录向上查找包含解决方案文件(DictGen.slnx)的根目录,找不到则回退到程序目录。</summary>
static string FindSolutionRoot(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        if (dir.EnumerateFiles("*.sln*").Any())
            return dir.FullName;
        dir = dir.Parent;
    }
    return startDir;
}

/// <summary>启动参数。</summary>
public sealed class RunFlags
{
    public bool IsSample { get; init; }
}