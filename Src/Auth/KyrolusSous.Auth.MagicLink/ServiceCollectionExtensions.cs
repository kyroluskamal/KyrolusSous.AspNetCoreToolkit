using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.MagicLink;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus passwordless magic link services with the in-memory token store.
    /// </summary>
    public static IServiceCollection AddKyrolusMagicLink(
        this IServiceCollection services,
        Action<KyrolusMagicLinkOptions>? configure = null)
    {
        var options = new KyrolusMagicLinkOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IKyrolusMagicLinkStore, KyrolusInMemoryMagicLinkStore>();
        services.TryAddSingleton<IKyrolusMagicLinkService, KyrolusMagicLinkService>();

        return services;
    }

    /// <summary>
    /// Registers a custom magic link persistence store (e.g. EF Core, Marten, Redis).
    /// </summary>
    public static IServiceCollection AddKyrolusMagicLinkStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
        this IServiceCollection services)
        where TStore : class, IKyrolusMagicLinkStore
    {
        services.Replace(ServiceDescriptor.Scoped<IKyrolusMagicLinkStore, TStore>());
        return services;
    }
}
