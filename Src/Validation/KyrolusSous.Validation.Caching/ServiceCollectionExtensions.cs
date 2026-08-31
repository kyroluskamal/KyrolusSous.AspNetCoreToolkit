namespace KyrolusSous.Validation.Caching;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default in-memory <see cref="IKyrolusValidationCacheStore"/> with one backed by an
    /// <see cref="IKyrolusCacheProvider"/> already registered in the container (e.g. via
    /// KyrolusSous.Caching.Redis's AddKyrolusRedisCaching), so validation caching is shared across app instances.
    /// </summary>
    public static IServiceCollection AddKyrolusValidationDistributedCache(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IKyrolusValidationCacheStore, KyrolusValidationDistributedCacheStore>());
        return services;
    }
}
