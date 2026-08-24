using DictGen.Abstractions.Models;

namespace DictGen.Abstractions;

/// <summary>数据库对象类型。</summary>
public enum SchemaObjectKind
{
    Table,
    View,
    Procedure,
}

/// <summary>
/// 流式传输的单个数据库对象。由 <see cref="ISchemaProvider.EnumerateObjectsAsync"/>
/// 逐对象产出,供生成器边读边生成。
/// </summary>
public sealed class SchemaObject
{
    public required SchemaObjectKind Kind { get; init; }

    public TableInfo? Table { get; init; }

    public ViewInfo? View { get; init; }

    public ProcedureInfo? Procedure { get; init; }
}
