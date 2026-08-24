namespace DictGen.Abstractions.Models;

/// <summary>数据表结构。</summary>
public sealed class TableInfo
{
    public required string Schema { get; init; }

    public required string Name { get; init; }

    /// <summary>表说明(MS_Description 扩展属性)。</summary>
    public string? Description { get; init; }

    /// <summary>估算行数(来自分区统计,非精确)。</summary>
    public long? RowCount { get; init; }

    public DateTime? CreatedAt { get; init; }

    public DateTime? ModifiedAt { get; init; }

    public IReadOnlyList<ColumnInfo> Columns { get; set; } = [];

    public IReadOnlyList<IndexInfo> Indexes { get; set; } = [];

    public IReadOnlyList<ForeignKeyInfo> ForeignKeys { get; set; } = [];
}
