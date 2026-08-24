namespace DictGen.Abstractions;

/// <summary>进度阶段。</summary>
public enum SchemaReadStage
{
    Unknown = 0,
    ReadingTables,
    ReadingViews,
    ReadingProcedures,
}

/// <summary>结构读取进度,由 <see cref="ISchemaProvider.EnumerateObjectsAsync"/> 通过 IProgress 报告。</summary>
public sealed record SchemaProgress(SchemaReadStage Stage, int Completed, int Total, int Percent);