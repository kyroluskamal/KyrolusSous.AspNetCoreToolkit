namespace KyrolusSous.Validation.Caching;

/// <summary>
/// DI registration for swapping the Validation runtime's default in-memory result cache for a distributed one.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default in-memory <see cref="IKyrolusValidationCacheStore"/> with one backed by an
    /// <see cref="IKyrolusCacheProvider"/> already registered in the container (e.g. via
    /// KyrolusSous.Caching.Redis's AddKyrolusRedisCaching), so validation caching is shared across app instances.
    /// Call after <c>KyrolusSous.Validation.Runtime</c>'s <c>AddKyrolusValidationRuntime()</c> (this uses
    /// <c>IServiceCollection.Replace(...)</c>, which requires the default registration to already exist).
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddKyrolusValidationRuntime();
    /// builder.Services.AddKyrolusRedisCaching(...); // registers IKyrolusCacheProvider
    /// builder.Services.AddKyrolusValidationDistributedCache();
    /// </code>
    /// </example>
    /// <param name="services">The service collection to modify.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddKyrolusValidationDistributedCache(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IKyrolusValidationCacheStore, KyrolusValidationDistributedCacheStore>());
        return services;
    }
}
