using DictGen.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DictGen.SqlServer;

public static class SqlServerServiceCollectionExtensions
{
    /// <summary>键控 DI 的注册键,对应 appsettings.json 的 Database:Provider。</summary>
    public const string ProviderKey = "SqlServer";

    /// <summary>连接串经 DatabaseOptions 从 DI 流入提供器。</summary>
    public static IServiceCollection AddSqlServerProvider(this IServiceCollection services)
    {
        services.AddKeyedSingleton<ISchemaProvider, SqlServerSchemaProvider>(ProviderKey);
        return services;
    }
}
