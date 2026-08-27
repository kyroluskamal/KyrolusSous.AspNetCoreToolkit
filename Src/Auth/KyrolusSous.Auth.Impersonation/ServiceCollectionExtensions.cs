using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.Impersonation;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus user impersonation services.
    /// </summary>
    public static IServiceCollection AddKyrolusImpersonation(this IServiceCollection services)
    {
        services.TryAddSingleton<IKyrolusImpersonationService, KyrolusImpersonationService>();
        return services;
    }
}
