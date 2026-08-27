using KyrolusSous.Auth.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.EntityFramework;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Entity Framework Core user store and lockout store implementations into the DI container.
    /// </summary>
    public static IServiceCollection AddKyrolusEfAuthStore<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<IKyrolusAuthUserStore, KyrolusEfAuthUserStore<TDbContext>>();
        services.AddScoped<IKyrolusAuthUserLockoutStore, KyrolusEfAuthUserStore<TDbContext>>();
        return services;
    }
}
