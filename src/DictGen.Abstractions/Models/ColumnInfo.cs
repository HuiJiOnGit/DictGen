namespace DictGen.Abstractions.Models;

/// <summary>列结构(表与视图共用)。</summary>
public sealed class ColumnInfo
{
    public required string Name { get; init; }

    /// <summary>列序号,从 1 开始。</summary>
    public int Ordinal { get; init; }

    /// <summary>基础类型名,如 nvarchar、int。</summary>
    public required string DataType { get; init; }

    /// <summary>完整显示类型,如 nvarchar(50)、decimal(18,2)、varchar(max)。由提供器计算。</summary>
    public required string DataTypeFull { get; init; }

    public bool IsNullable { get; init; }

    public bool IsIdentity { get; init; }

    public bool IsComputed { get; init; }

    public bool IsPrimaryKey { get; init; }

    /// <summary>默认值定义,如 ((0))、(getdate())。</summary>
    public string? DefaultValue { get; init; }

    /// <summary>列说明(MS_Description 扩展属性)。</summary>
    public string? Description { get; init; }

    public string? Collation { get; init; }
}
