using System.Text.Json;
using KyrolusSous.Caching.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Repositories.EF.Cache.Distributed;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusEfDistributedCacheProvider(
        this IServiceCollection services,
        Action<JsonSerializerOptions>? configureJson = null)
    {
        services.TryAddSingleton<IKyrolusCacheProvider>(sp =>
        {
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            configureJson?.Invoke(json);
            return new KyrolusEfDistributedCacheProvider(sp.GetRequiredService<IDistributedCache>(), json);
        });

        return services;
    }
}
