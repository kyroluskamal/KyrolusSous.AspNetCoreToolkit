using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// Extension methods for registering and configuring mandatory Correlation Tracking in EndpointKit.
/// </summary>
public static class CorrelationApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the mandatory Correlation services and auto-injects <see cref="KyrolusCorrelationStartupFilter"/>
    /// so the middleware executes automatically at the head of the ASP.NET Core pipeline.
    /// </summary>
    public static IServiceCollection AddKyrolusCorrelation(
        this IServiceCollection services,
        Action<KyrolusCorrelationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<KyrolusCorrelationOptions>();
        }

        // Register startup filter to guarantee automatic execution at the top of the middleware pipeline
        services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, KyrolusCorrelationStartupFilter>());

        return services;
    }

    /// <summary>
    /// Adds <see cref="KyrolusCorrelationMiddleware"/> explicitly into the ASP.NET Core request pipeline.
    /// Note: When <c>AddKyrolus</c> or <c>AddKyrolusCorrelation</c> is used, this is also injected automatically via <see cref="IStartupFilter"/>.
    /// </summary>
    public static IApplicationBuilder UseKyrolusCorrelation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<KyrolusCorrelationMiddleware>();
    }
}
