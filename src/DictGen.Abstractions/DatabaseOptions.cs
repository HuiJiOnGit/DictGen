namespace DictGen.Abstractions;

/// <summary>数据库连接选项,对应 appsettings.json 的 Database 节。</summary>
public sealed class DatabaseOptions
{
    /// <summary>提供器键名,对应键控 DI 注册,如 "SqlServer"。</summary>
    public string Provider { get; set; } = "SqlServer";

    public string ConnectionString { get; set; } = "";
}
