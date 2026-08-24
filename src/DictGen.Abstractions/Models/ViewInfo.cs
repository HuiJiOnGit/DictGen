namespace DictGen.Abstractions.Models;

/// <summary>视图结构。</summary>
public sealed class ViewInfo
{
    /// <summary>数据库内部对象 ID(仅提供器内部用于关联,不输出到产物)。</summary>
    public int ObjectId { get; set; }

    public required string Schema { get; init; }

    public required string Name { get; init; }

    /// <summary>视图说明(MS_Description 扩展属性)。</summary>
    public string? Description { get; init; }

    /// <summary>视图定义(CREATE VIEW 文本)。</summary>
    public string? Definition { get; set; }

    public IReadOnlyList<ColumnInfo> Columns { get; set; } = [];
}
