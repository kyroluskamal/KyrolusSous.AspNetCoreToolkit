using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// Mandatory startup filter that injects <see cref="KyrolusCorrelationMiddleware"/> at the very start
/// of the ASP.NET Core request processing pipeline, ensuring correlation tracking is active automatically
/// without requiring manual developer configuration.
/// </summary>
public sealed class KyrolusCorrelationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseKyrolusCorrelation();
            next(app);
        };
    }
}
