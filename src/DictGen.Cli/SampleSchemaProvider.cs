using System.Runtime.CompilerServices;
using DictGen.Abstractions;
using DictGen.Abstractions.Models;

namespace DictGen.Cli;

/// <summary>
/// 内置示例数据库结构提供器,用于 --sample 模式:不连接任何数据库即可验证完整生成管线。
/// </summary>
public sealed class SampleSchemaProvider : ISchemaProvider, ISchemaInfoProvider
{
    public const string ProviderKey = "Sample";

    private DatabaseSchema? _schema;
    private object _lock = new();

    public Task<SchemaSourceInfo> GetSourceInfoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new SchemaSourceInfo
        {
            DatabaseName = "SampleDB",
            ServerName = "本地示例",
            ServerVersion = "SQL Server 16.0.4100.1 (示例)",
        });

    private DatabaseSchema Build()
    {
        if (_schema is not null) return _schema;
        lock (_lock)
        {
            if (_schema is not null) return _schema;
            _schema = new DatabaseSchema
            {
                DatabaseName = "SampleDB", ServerName = "本地示例", ServerVersion = "SQL Server 16.0.4100.1 (示例)",
                Tables = [.. BuildTables()], Views = [.. BuildViews()], Procedures = [.. BuildProcedures()],
            };
            return _schema;
        }
    }

    public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default)
        => Task.FromResult(Build());

    public async IAsyncEnumerable<SchemaObject> EnumerateObjectsAsync(
        IProgress<SchemaProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var t in Build().Tables)
        {
            yield return new SchemaObject { Kind = SchemaObjectKind.Table, Table = t };
            ct.ThrowIfCancellationRequested();
        }
        foreach (var v in Build().Views)
        {
            yield return new SchemaObject { Kind = SchemaObjectKind.View, View = v };
            ct.ThrowIfCancellationRequested();
        }
        foreach (var p in Build().Procedures)
        {
            yield return new SchemaObject { Kind = SchemaObjectKind.Procedure, Procedure = p };
            ct.ThrowIfCancellationRequested();
        }
    }

    private static IEnumerable<TableInfo> BuildTables()
    {
        yield return new TableInfo
        {
            Schema = "dbo", Name = "Users", Description = "用户表(示例)", RowCount = 128,
            CreatedAt = new DateTime(2023, 1, 10), ModifiedAt = new DateTime(2025, 6, 1),
            Columns =
            [
                new ColumnInfo { Name = "Id", Ordinal = 1, DataType = "int", DataTypeFull = "INT", IsIdentity = true, IsPrimaryKey = true, Description = "主键ID" },
                new ColumnInfo { Name = "UserName", Ordinal = 2, DataType = "nvarchar", DataTypeFull = "NVARCHAR(50)", Description = "登录名" },
                new ColumnInfo { Name = "NickName", Ordinal = 3, DataType = "nvarchar", DataTypeFull = "NVARCHAR(50)", IsNullable = true, Description = "昵称" },
                new ColumnInfo { Name = "Email", Ordinal = 4, DataType = "nvarchar", DataTypeFull = "NVARCHAR(100)", IsNullable = true },
                new ColumnInfo { Name = "Status", Ordinal = 5, DataType = "tinyint", DataTypeFull = "TINYINT", DefaultValue = "((1))", Description = "1-正常 0-禁用" },
                new ColumnInfo { Name = "CreatedAt", Ordinal = 6, DataType = "datetime", DataTypeFull = "DATETIME", DefaultValue = "(getdate())", Description = "创建时间" },
            ],
            Indexes =
            [
                new IndexInfo { Name = "PK_Users", IsPrimaryKey = true, IsUnique = true, Type = "CLUSTERED", Columns = ["Id"] },
                new IndexInfo { Name = "IX_Users_UserName", IsUnique = true, Type = "NONCLUSTERED", Columns = ["UserName"] },
                new IndexInfo { Name = "IX_Users_Status", Type = "NONCLUSTERED", Columns = ["Status"], IncludedColumns = ["NickName"] },
            ],
        };
        yield return new TableInfo
        {
            Schema = "dbo", Name = "Orders", Description = "订单表(示例)", RowCount = 45678,
            CreatedAt = new DateTime(2023, 3, 15),
            Columns =
            [
                new ColumnInfo { Name = "Id", Ordinal = 1, DataType = "bigint", DataTypeFull = "BIGINT", IsIdentity = true, IsPrimaryKey = true, Description = "订单ID" },
                new ColumnInfo { Name = "UserId", Ordinal = 2, DataType = "int", DataTypeFull = "INT", Description = "下单用户" },
                new ColumnInfo { Name = "TotalAmount", Ordinal = 3, DataType = "decimal", DataTypeFull = "DECIMAL(18,2)", Description = "订单金额" },
                new ColumnInfo { Name = "Status", Ordinal = 4, DataType = "tinyint", DataTypeFull = "TINYINT", DefaultValue = "((0))", Description = "0-待支付 1-已支付 2-已取消" },
                new ColumnInfo { Name = "Remark", Ordinal = 5, DataType = "nvarchar", DataTypeFull = "NVARCHAR(500)", IsNullable = true, Description = "备注" },
                new ColumnInfo { Name = "CreatedAt", Ordinal = 6, DataType = "datetime", DataTypeFull = "DATETIME", DefaultValue = "(getdate())" },
            ],
            Indexes =
            [
                new IndexInfo { Name = "PK_Orders", IsPrimaryKey = true, IsUnique = true, Type = "CLUSTERED", Columns = ["Id"] },
                new IndexInfo { Name = "IX_Orders_UserId", Type = "NONCLUSTERED", Columns = ["UserId"] },
                new IndexInfo { Name = "IX_Orders_Status_CreatedAt", Type = "NONCLUSTERED", Columns = ["Status", "CreatedAt"] },
            ],
            ForeignKeys = [new ForeignKeyInfo { Name = "FK_Orders_Users", ReferencedTable = "dbo.Users", Columns = [new ForeignKeyColumn("UserId", "Id")], OnDeleteAction = "NO_ACTION", OnUpdateAction = "NO_ACTION" }],
        };
        yield return new TableInfo
        {
            Schema = "dbo", Name = "OrderItems", Description = "订单明细(示例)", RowCount = 234567,
            CreatedAt = new DateTime(2023, 3, 15),
            Columns =
            [
                new ColumnInfo { Name = "Id", Ordinal = 1, DataType = "bigint", DataTypeFull = "BIGINT", IsIdentity = true, IsPrimaryKey = true },
                new ColumnInfo { Name = "OrderId", Ordinal = 2, DataType = "bigint", DataTypeFull = "BIGINT", Description = "所属订单" },
                new ColumnInfo { Name = "ProductName", Ordinal = 3, DataType = "nvarchar", DataTypeFull = "NVARCHAR(200)", Description = "商品名称" },
                new ColumnInfo { Name = "Price", Ordinal = 4, DataType = "decimal", DataTypeFull = "DECIMAL(18,2)", Description = "单价" },
                new ColumnInfo { Name = "Qty", Ordinal = 5, DataType = "int", DataTypeFull = "INT", DefaultValue = "((1))", Description = "数量" },
                new ColumnInfo { Name = "LineTotal", Ordinal = 6, DataType = "decimal", DataTypeFull = "DECIMAL(18,2)", IsComputed = true, Description = "计算列: 单价×数量" },
            ],
            Indexes =
            [
                new IndexInfo { Name = "PK_OrderItems", IsPrimaryKey = true, IsUnique = true, Type = "CLUSTERED", Columns = ["Id"] },
                new IndexInfo { Name = "IX_OrderItems_OrderId", Type = "NONCLUSTERED", Columns = ["OrderId"] },
            ],
            ForeignKeys = [new ForeignKeyInfo { Name = "FK_OrderItems_Orders", ReferencedTable = "dbo.Orders", Columns = [new ForeignKeyColumn("OrderId", "Id")], OnDeleteAction = "CASCADE", OnUpdateAction = "NO_ACTION" }],
        };
    }

    private static IEnumerable<ViewInfo> BuildViews()
    {
        yield return new ViewInfo
        {
            Schema = "dbo", Name = "V_OrderSummary", Description = "订单汇总视图(示例)",
            Columns =
            [
                new ColumnInfo { Name = "OrderId", Ordinal = 1, DataType = "bigint", DataTypeFull = "BIGINT" },
                new ColumnInfo { Name = "UserName", Ordinal = 2, DataType = "nvarchar", DataTypeFull = "NVARCHAR(50)" },
                new ColumnInfo { Name = "TotalAmount", Ordinal = 3, DataType = "decimal", DataTypeFull = "DECIMAL(18,2)" },
                new ColumnInfo { Name = "ItemCount", Ordinal = 4, DataType = "int", DataTypeFull = "INT" },
            ],
            Definition = """
                CREATE VIEW [dbo].[V_OrderSummary] AS
                SELECT o.Id AS OrderId, u.UserName, o.TotalAmount, COUNT(i.Id) AS ItemCount
                FROM dbo.Orders o JOIN dbo.Users u ON u.Id = o.UserId
                LEFT JOIN dbo.OrderItems i ON i.OrderId = o.Id GROUP BY o.Id, u.UserName, o.TotalAmount
                """,
        };
    }

    private static IEnumerable<ProcedureInfo> BuildProcedures()
    {
        yield return new ProcedureInfo
        {
            Schema = "dbo", Name = "SP_GetUserOrders", Description = "查询用户订单(示例)",
            Parameters =
            [
                new ParameterInfo { Name = "@UserId", Ordinal = 1, DataTypeFull = "INT", Direction = "IN" },
                new ParameterInfo { Name = "@Status", Ordinal = 2, DataTypeFull = "TINYINT", Direction = "IN", DefaultValue = "NULL" },
                new ParameterInfo { Name = "@Total", Ordinal = 3, DataTypeFull = "INT", Direction = "OUT" },
            ],
            Definition = """
                CREATE PROCEDURE [dbo].[SP_GetUserOrders] @UserId INT, @Status TINYINT = NULL, @Total INT OUTPUT AS
                BEGIN SET NOCOUNT ON; SELECT @Total=COUNT(*) FROM dbo.Orders WHERE UserId=@UserId AND (@Status IS NULL OR Status=@Status);
                SELECT o.Id, o.TotalAmount, o.Status, o.CreatedAt FROM dbo.Orders o WHERE o.UserId=@UserId AND (@Status IS NULL OR o.Status=@Status) ORDER BY o.CreatedAt DESC; END
                """,
        };
        yield return new ProcedureInfo
        {
            Schema = "dbo", Name = "SP_CancelExpiredOrders", Description = "批量取消超时未支付订单(示例)",
            Parameters =
            [
                new ParameterInfo { Name = "@TimeoutMinutes", Ordinal = 1, DataTypeFull = "INT", Direction = "IN", DefaultValue = "30" },
                new ParameterInfo { Name = "@Affected", Ordinal = 2, DataTypeFull = "INT", Direction = "OUT" },
            ],
            Definition = """
                CREATE PROCEDURE [dbo].[SP_CancelExpiredOrders] @TimeoutMinutes INT=30, @Affected INT OUTPUT AS
                BEGIN SET NOCOUNT ON; UPDATE dbo.Orders SET Status=2 WHERE Status=0 AND CreatedAt<DATEADD(MINUTE,-@TimeoutMinutes,GETDATE()); SET @Affected=@@ROWCOUNT; END
                """,
        };
    }
}
