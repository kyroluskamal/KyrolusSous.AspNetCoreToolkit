using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.Sessions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus session management services with the default in-memory session store.
    /// </summary>
    public static IServiceCollection AddKyrolusSessions(
        this IServiceCollection services,
        Action<KyrolusSessionOptions>? configure = null)
    {
        var options = new KyrolusSessionOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IKyrolusSessionStore, KyrolusInMemorySessionStore>();
        services.TryAddSingleton<IKyrolusSessionManager, KyrolusSessionManager>();

        return services;
    }

    /// <summary>
    /// Registers a custom session persistence store (e.g. Redis, EF Core, Marten).
    /// </summary>
    public static IServiceCollection AddKyrolusSessionStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
        this IServiceCollection services)
        where TStore : class, IKyrolusSessionStore
    {
        services.Replace(ServiceDescriptor.Scoped<IKyrolusSessionStore, TStore>());
        return services;
    }
}
