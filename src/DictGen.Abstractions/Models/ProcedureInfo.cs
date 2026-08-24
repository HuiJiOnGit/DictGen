namespace DictGen.Abstractions.Models;

/// <summary>存储过程结构。</summary>
public sealed class ProcedureInfo
{
    /// <summary>数据库内部对象 ID(仅提供器内部用于关联,不输出到产物)。</summary>
    public int ObjectId { get; set; }

    public required string Schema { get; init; }

    public required string Name { get; init; }

    /// <summary>存储过程说明(MS_Description 扩展属性)。</summary>
    public string? Description { get; init; }

    /// <summary>过程体定义(CREATE PROCEDURE 文本)。</summary>
    public string? Definition { get; set; }

    public IReadOnlyList<ParameterInfo> Parameters { get; set; } = [];
}

/// <summary>存储过程参数。</summary>
public sealed class ParameterInfo
{
    public required string Name { get; init; }

    /// <summary>参数序号,从 1 开始(返回值占 0)。</summary>
    public int Ordinal { get; init; }

    /// <summary>完整显示类型,如 nvarchar(50)、int。</summary>
    public required string DataTypeFull { get; init; }

    /// <summary>IN / OUT / INOUT。</summary>
    public string? Direction { get; init; }

    public string? DefaultValue { get; init; }
}
