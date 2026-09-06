using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.TokenRevocation;

/// <summary>
/// Extension methods for registering Kyrolus token revocation services and blacklist stores in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus token revocation and blacklist services with the in-memory blacklist.
    /// </summary>
    public static IServiceCollection AddKyrolusTokenRevocation(this IServiceCollection services)
    {
        services.TryAddSingleton<IKyrolusTokenBlacklist, KyrolusInMemoryTokenBlacklist>();
        return services;
    }

    /// <summary>
    /// Registers a custom token blacklist implementation (e.g. Redis, distributed cache).
    /// </summary>
    public static IServiceCollection AddKyrolusTokenBlacklist<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TBlacklist>(
        this IServiceCollection services)
        where TBlacklist : class, IKyrolusTokenBlacklist
    {
        services.Replace(ServiceDescriptor.Singleton<IKyrolusTokenBlacklist, TBlacklist>());
        return services;
    }

    /// <summary>
    /// Registers the distributed cache-backed token blacklist using <see cref="KyrolusSous.Caching.Abstractions.IKyrolusCacheProvider"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusCacheTokenBlacklist(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IKyrolusTokenBlacklist, KyrolusCacheTokenBlacklist>());
        return services;
    }
}
