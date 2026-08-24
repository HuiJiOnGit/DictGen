using DictGen.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DictGen.EFCore.SqlServer;

public static class SqlServerServiceCollectionExtensions
{
    /// <summary>键控 DI 的注册键,对应 appsettings.json 的 Database:Provider。</summary>
    public const string ProviderKey = "SqlServer";

    public static IServiceCollection AddSqlServerProvider(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<SchemaDbContext>(options => options.UseSqlServer(connectionString));
        services.AddKeyedSingleton<ISchemaProvider, SqlServerSchemaProvider>(ProviderKey);
        return services;
    }
}
