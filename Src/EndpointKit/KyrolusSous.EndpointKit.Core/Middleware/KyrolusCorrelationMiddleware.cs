using System.Diagnostics;
using KyrolusSous.EndpointKit.Core.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// Mandatory Inbound HTTP Middleware that captures or generates the Correlation ID at the ingress boundary,
/// attaches it to the ambient context, tags the OpenTelemetry Activity, and ensures it is echoed in the response headers.
/// </summary>
public sealed class KyrolusCorrelationMiddleware
{
    private const string ExecutedMarkerKey = "__KyrolusCorrelationExecuted";
    private readonly RequestDelegate _next;
    private readonly KyrolusCorrelationOptions _options;

    public KyrolusCorrelationMiddleware(RequestDelegate next, IOptions<KyrolusCorrelationOptions>? options = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? new KyrolusCorrelationOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Guard against duplicate execution if registered both via IStartupFilter and manual app.Use
        if (context.Items.ContainsKey(ExecutedMarkerKey))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Items[ExecutedMarkerKey] = true;

        var correlationId = ResolveOrGenerateCorrelationId(context);
        var tenantId = ResolveTenantId(context);

        // 1. Store in HttpContext.Items
        context.Items[_options.ItemKey] = correlationId;

        // 2. Echo back in Response Headers for client-side traceability
        if (_options.IncludeInResponse && !context.Response.Headers.ContainsKey(_options.HeaderName))
        {
            context.Response.Headers.TryAdd(_options.HeaderName, correlationId);
        }

        // 3. Enrich W3C Activity if active
        if (_options.SetActivityTag && Activity.Current is not null)
        {
            Activity.Current.SetTag("correlation.id", correlationId);
        }

        // 4. Set ambient async-local scope for all downstream components (CQRS, Handlers, Logging)
        using (KyrolusCorrelationContext.BeginScope(correlationId, tenantId, context.User.Identity?.Name))
        {
            await _next(context).ConfigureAwait(false);
        }
    }

    private string ResolveOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(_options.HeaderName, out var headerVal) &&
            !string.IsNullOrWhiteSpace(headerVal))
        {
            return headerVal.ToString();
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private static string? ResolveTenantId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantVal) &&
            !string.IsNullOrWhiteSpace(tenantVal))
        {
            return tenantVal.ToString();
        }

        return null;
    }
}
