using DictGen.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DictGen.Generators.StaticSite;

public static class StaticSiteServiceCollectionExtensions
{
    /// <summary>键控 DI 的注册键,对应 appsettings.json 的 Generation:Generator。</summary>
    public const string GeneratorKey = "StaticSite";

    public static IServiceCollection AddStaticSiteGenerator(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IDataDictionaryGenerator, StaticSiteGenerator>(GeneratorKey);
        return services;
    }
}
