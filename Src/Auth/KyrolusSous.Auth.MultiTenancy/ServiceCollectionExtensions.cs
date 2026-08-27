using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.MultiTenancy;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus multi-tenancy context and default resolvers (Header, Claim, Subdomain).
    /// </summary>
    public static IServiceCollection AddKyrolusMultiTenancy(this IServiceCollection services)
    {
        services.TryAddScoped<IKyrolusTenantContext, KyrolusTenantContext>();

        // Register default resolvers
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusTenantResolver, KyrolusHeaderTenantResolver>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusTenantResolver, KyrolusClaimTenantResolver>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusTenantResolver, KyrolusSubdomainTenantResolver>());

        // Composite resolver chains all registered resolvers
        services.TryAddScoped<IKyrolusTenantResolver, KyrolusCompositeTenantResolver>();

        return services;
    }

    /// <summary>
    /// Adds middleware to resolve the ambient tenant context on each HTTP request.
    /// </summary>
    public static IApplicationBuilder UseKyrolusMultiTenancy(this IApplicationBuilder app)
    {
        return app.UseMiddleware<KyrolusTenantResolutionMiddleware>();
    }
}
