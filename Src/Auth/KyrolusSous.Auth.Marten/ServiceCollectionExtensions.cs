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
        return services;
    }
}
