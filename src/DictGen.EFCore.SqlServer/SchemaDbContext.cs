using Microsoft.EntityFrameworkCore;

namespace DictGen.EFCore.SqlServer;

/// <summary>
/// 仅用于执行模式读取的原生 SQL 查询,不映射任何实体。
/// 通过 IDbContextFactory 按需创建、用完即弃。
/// </summary>
public sealed class SchemaDbContext(DbContextOptions<SchemaDbContext> options) : DbContext(options);
