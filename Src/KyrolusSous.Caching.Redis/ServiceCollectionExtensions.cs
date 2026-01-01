using KyrolusSous.Caching.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Caching.Redis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusRedisCacheProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<ICacheProvider, RedisCacheProvider>();
        return services;
    }
}
