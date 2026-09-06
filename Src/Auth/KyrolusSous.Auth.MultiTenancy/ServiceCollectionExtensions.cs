using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.MultiTenancy;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus multi-tenancy context and default resolvers using a hardened resolution strategy
    /// (Authenticated Claims -> Host Subdomain -> Optional Restricted Header).
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional configuration action to adjust multi-tenancy options.</param>
    public static IServiceCollection AddKyrolusMultiTenancy(
        this IServiceCollection services,
        Action<KyrolusMultiTenancyOptions>? configure = null)
    {
        var options = new KyrolusMultiTenancyOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddScoped<IKyrolusTenantContext, KyrolusTenantContext>();

        // Hardened priority order:
        // 1. Authenticated user token claim (unspoofable, cryptographically signed)
        services.TryAddSingleton(new KyrolusClaimTenantResolver(options.ClaimType));

        // 2. Client host subdomain (tamper-resistant DNS host)
        services.TryAddSingleton<KyrolusSubdomainTenantResolver>();

        // 3. Client HTTP header (only if explicitly enabled to prevent tenant spoofing)
        if (options.AllowHeaderResolution)
        {
            services.TryAddSingleton(new KyrolusHeaderTenantResolver(options.HeaderName));
        }

        // Composite resolver chains all registered resolvers in hardened priority order
        services.TryAddScoped<IKyrolusTenantResolver>(sp =>
        {
            var resolvers = new List<IKyrolusTenantResolver>
            {
                sp.GetRequiredService<KyrolusClaimTenantResolver>(),
                sp.GetRequiredService<KyrolusSubdomainTenantResolver>()
            };

            if (options.AllowHeaderResolution)
            {
                resolvers.Add(sp.GetRequiredService<KyrolusHeaderTenantResolver>());
            }

            return new KyrolusCompositeTenantResolver(resolvers);
        });

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
