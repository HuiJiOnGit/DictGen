namespace DictGen.Abstractions.Models;

/// <summary>外键约束。</summary>
public sealed class ForeignKeyInfo
{
    public required string Name { get; init; }

    /// <summary>引用表(格式 Schema.Table)。</summary>
    public required string ReferencedTable { get; init; }

    /// <summary>列映射,顺序与约束定义一致。</summary>
    public IReadOnlyList<ForeignKeyColumn> Columns { get; init; } = [];

    public string? OnDeleteAction { get; init; }

    public string? OnUpdateAction { get; init; }
}

/// <summary>外键的列对(本表列 → 引用表列)。</summary>
public sealed record ForeignKeyColumn(string Column, string ReferencedColumn);
