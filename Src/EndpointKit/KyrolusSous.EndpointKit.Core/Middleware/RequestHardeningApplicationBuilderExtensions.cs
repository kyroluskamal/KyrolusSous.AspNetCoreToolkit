using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// Extension methods for registering and configuring request hardening security middleware in EndpointKit.
/// </summary>
public static class RequestHardeningApplicationBuilderExtensions
{
    /// <summary>
    /// Registers configuration options for HTTP request hardening into the dependency injection container.
    /// </summary>
    public static IServiceCollection AddKyrolusRequestHardening(
        this IServiceCollection services,
        Action<KyrolusRequestHardeningOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<KyrolusRequestHardeningOptions>();
        }

        return services;
    }

    /// <summary>
    /// Adds <see cref="KyrolusRequestHardeningMiddleware"/> into the ASP.NET Core request pipeline
    /// to defend standalone web APIs against path traversal, method override spoofing, and header flood DoS attacks.
    /// </summary>
    public static IApplicationBuilder UseKyrolusRequestHardening(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<KyrolusRequestHardeningMiddleware>();
    }
}
