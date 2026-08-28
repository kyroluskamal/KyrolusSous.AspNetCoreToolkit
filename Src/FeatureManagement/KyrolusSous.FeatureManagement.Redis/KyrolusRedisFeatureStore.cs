using KyrolusSous.FeatureManagement.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KyrolusSous.FeatureManagement.Redis;

public sealed class KyrolusRedisFeatureStore(IConnectionMultiplexer redis) : IKyrolusFeatureStore
{
    private const string HashKey = "kyrolus:features:states";
    private readonly IDatabase _db = (redis ?? throw new ArgumentNullException(nameof(redis))).GetDatabase();

    public async Task<bool?> GetFeatureStateAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var val = await _db.HashGetAsync(HashKey, featureName).ConfigureAwait(false);
        if (val.HasValue && bool.TryParse(val, out var enabled))
        {
            return enabled;
        }
        return null;
    }

    public async Task SetFeatureStateAsync(string featureName, bool enabled, CancellationToken cancellationToken = default)
    {
        await _db.HashSetAsync(HashKey, featureName, enabled.ToString()).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _db.HashGetAllAsync(HashKey).ConfigureAwait(false);
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry.Name.HasValue && entry.Value.HasValue && bool.TryParse(entry.Value, out var enabled))
            {
                result[entry.Name!] = enabled;
            }
        }

        return result;
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusRedisFeatureStore(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusFeatureStore, KyrolusRedisFeatureStore>();
        return services;
    }
}
