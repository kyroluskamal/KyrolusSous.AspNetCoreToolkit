using KyrolusSous.Auth.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Marten;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Marten user store and lockout store implementations into the DI container.
    /// </summary>
    public static IServiceCollection AddKyrolusMartenAuthStore(this IServiceCollection services)
    {
        services.AddScoped<IKyrolusAuthUserStore, KyrolusMartenAuthUserStore>();
        services.AddScoped<IKyrolusAuthUserLockoutStore, KyrolusMartenAuthUserStore>();
        services.AddScoped<TokenRevocation.IKyrolusTokenBlacklist, KyrolusMartenTokenBlacklist>();
        services.AddScoped<Sessions.IKyrolusSessionStore, KyrolusMartenSessionStore>();
        services.AddScoped<MagicLink.IKyrolusMagicLinkStore, KyrolusMartenMagicLinkStore>();
        return services;
    }

    public static IServiceCollection AddKyrolusMartenTokenBlacklist(this IServiceCollection services)
    {
        services.AddScoped<TokenRevocation.IKyrolusTokenBlacklist, KyrolusMartenTokenBlacklist>();
        return services;
    }

    public static IServiceCollection AddKyrolusMartenSessionStore(this IServiceCollection services)
    {
        services.AddScoped<Sessions.IKyrolusSessionStore, KyrolusMartenSessionStore>();
        return services;
    }

    public static IServiceCollection AddKyrolusMartenMagicLinkStore(this IServiceCollection services)
    {
        services.AddScoped<MagicLink.IKyrolusMagicLinkStore, KyrolusMartenMagicLinkStore>();
        return services;
    }
}
