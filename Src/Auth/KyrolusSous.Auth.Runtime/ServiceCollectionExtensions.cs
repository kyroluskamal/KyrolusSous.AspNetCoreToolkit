using System.Diagnostics.CodeAnalysis;
using KyrolusSous.Auth.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Auth.Runtime;

/// <summary>
/// Registration helpers for the Kyrolus auth runtime.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default password hasher, sign-in policy, claims-principal factory and
    /// external-login handler.
    /// </summary>
    /// <remarks>
    /// Every registration uses <c>TryAdd</c>, so an application that has already supplied its own
    /// implementation of any of these keeps it. The one thing this does <em>not</em> register is
    /// an <see cref="IKyrolusAuthUserStore"/> - that is the application's to provide, and
    /// pretending otherwise is what couples an auth library to a database.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optionally configures the auth runtime options.</param>
    public static IServiceCollection AddKyrolusAuthCore(
        this IServiceCollection services,
        Action<KyrolusAuthOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<KyrolusAuthOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<KyrolusAuthOptions>, KyrolusAuthOptionsValidator>());
        optionsBuilder.ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IKyrolusPasswordHasher, KyrolusPbkdf2PasswordHasher>();
        services.TryAddSingleton<IKyrolusClaimsPrincipalFactory, KyrolusClaimsPrincipalFactory>();
        services.TryAddScoped<IKyrolusUserAuthenticator, KyrolusUserAuthenticator>();
        services.TryAddScoped<IKyrolusExternalLoginHandler, KyrolusExternalLoginHandler>();

        return services;
    }

    /// <summary>
    /// Registers the application's user store.
    /// </summary>
    /// <typeparam name="TStore">The store implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime. Defaults to scoped, which is what a store backed by a database wants.</param>
    public static IServiceCollection AddKyrolusAuthUserStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TStore : class, IKyrolusAuthUserStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(ServiceDescriptor.Describe(typeof(IKyrolusAuthUserStore), typeof(TStore), lifetime));
        return services;
    }

    /// <summary>
    /// Registers the in-memory user store for development, samples and tests, and its matching
    /// lockout store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="seed">Optionally seeds users into the store as it is created.</param>
    public static IServiceCollection AddKyrolusInMemoryAuthUserStore(
        this IServiceCollection services,
        Action<KyrolusInMemoryAuthUserStore>? seed = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(_ =>
        {
            var store = new KyrolusInMemoryAuthUserStore();
            seed?.Invoke(store);
            return store;
        });

        services.TryAddSingleton<IKyrolusAuthUserStore>(
            sp => sp.GetRequiredService<KyrolusInMemoryAuthUserStore>());
        services.TryAddSingleton<IKyrolusAuthUserLockoutStore>(
            sp => sp.GetRequiredService<KyrolusInMemoryAuthUserStore>());

        return services;
    }
}
