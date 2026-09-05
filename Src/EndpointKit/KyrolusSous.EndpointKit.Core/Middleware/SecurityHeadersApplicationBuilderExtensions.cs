using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// Extension methods for registering and configuring HTTP security response headers in EndpointKit.
/// </summary>
public static class SecurityHeadersApplicationBuilderExtensions
{
    /// <summary>
    /// Registers configuration options for HTTP security response headers into the dependency injection container.
    /// </summary>
    public static IServiceCollection AddKyrolusSecurityHeaders(
        this IServiceCollection services,
        Action<KyrolusSecurityHeadersOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<KyrolusSecurityHeadersOptions>();
        }

        return services;
    }

    /// <summary>
    /// Adds <see cref="KyrolusSecurityHeadersMiddleware"/> into the ASP.NET Core request pipeline
    /// to enforce strict security response headers (X-Frame-Options, X-Content-Type-Options, etc.).
    /// </summary>
    public static IApplicationBuilder UseKyrolusSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<KyrolusSecurityHeadersMiddleware>();
    }
}
