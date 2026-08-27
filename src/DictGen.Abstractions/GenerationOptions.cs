namespace DictGen.Abstractions;

/// <summary>数据字典生成选项,对应 appsettings.json 的 Generation 节。</summary>
public sealed class GenerationOptions
{
    /// <summary>产物输出目录。</summary>
    public string OutputDirectory { get; set; } = "output";

    /// <summary>站点标题。</summary>
    public string SiteTitle { get; set; } = "数据库数据字典";

    /// <summary>生成器键名,对应键控 DI 注册,如 "StaticSite"。</summary>
    public string Generator { get; set; } = "StaticSite";

    public bool IncludeTables { get; set; } = true;

    public bool IncludeViews { get; set; } = true;

    public bool IncludeProcedures { get; set; } = true;

    /// <summary>是否读取视图/存储过程定义文本。</summary>
    public bool IncludeObjectDefinitions { get; set; } = true;

    /// <summary>是否同时输出全嵌入单文件 HTML(便于拷贝分发)。为 true 时索引页即为自包含文件。</summary>
    public bool EmbedSingleFile { get; set; }

    /// <summary>生成前是否清空输出目录。</summary>
    public bool CleanOutputDirectory { get; set; } = true;

    /// <summary>只生成指定 schema 下的对象;为空表示全部。</summary>
    public List<string> SchemaFilter { get; set; } = [];
}
