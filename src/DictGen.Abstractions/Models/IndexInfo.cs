namespace DictGen.Abstractions.Models;

/// <summary>索引定义。</summary>
public sealed class IndexInfo
{
    public required string Name { get; init; }

    public bool IsUnique { get; init; }

    public bool IsPrimaryKey { get; init; }

    /// <summary>CLUSTERED / NONCLUSTERED。</summary>
    public string? Type { get; init; }

    /// <summary>筛选索引的过滤器表达式,如 ([Status]&lt;&gt;(0))。</summary>
    public string? Filter { get; init; }

    /// <summary>索引键列(按 key_ordinal 排序)。</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>包含列。</summary>
    public IReadOnlyList<string> IncludedColumns { get; init; } = [];
}
