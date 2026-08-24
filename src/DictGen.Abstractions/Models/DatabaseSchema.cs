namespace DictGen.Abstractions.Models;

/// <summary>数据库的完整结构信息,由 <see cref="ISchemaProvider"/> 读取、由生成器消费。数据库无关的中性模型。</summary>
public sealed class DatabaseSchema
{
    public required string DatabaseName { get; init; }

    public required string ServerName { get; init; }

    public string? ServerVersion { get; init; }

    public DateTime GeneratedAt { get; init; } = DateTime.Now;

    public IReadOnlyList<TableInfo> Tables { get; init; } = [];

    public IReadOnlyList<ViewInfo> Views { get; init; } = [];

    public IReadOnlyList<ProcedureInfo> Procedures { get; init; } = [];

    public int TotalObjectCount => Tables.Count + Views.Count + Procedures.Count;
}
