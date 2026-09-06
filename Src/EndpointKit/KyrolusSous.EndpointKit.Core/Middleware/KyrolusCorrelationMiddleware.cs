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
            var raw = headerVal.ToString();
            if (IsValidCorrelationId(raw))
            {
                return raw;
            }
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private static bool IsValidCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static string? ResolveTenantId(HttpContext context)
    {
        // 1. Authoritative ambient context item if already populated
        if (context.Items.TryGetValue("KyrolusTenantId", out var itemVal) && itemVal is string itemTenant && !string.IsNullOrWhiteSpace(itemTenant))
        {
            return itemTenant;
        }

        // 2. Authoritative authenticated JWT claim if available
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claimTenant = context.User.FindFirst("tenant_id")?.Value
                           ?? context.User.FindFirst("tenant")?.Value;

            if (!string.IsNullOrWhiteSpace(claimTenant) && IsValidTenantIdentifier(claimTenant))
            {
                return claimTenant;
            }
        }

        // 3. Client header with strict format validation (CWE-639 tenant spoofing defense)
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantVal) &&
            !string.IsNullOrWhiteSpace(tenantVal))
        {
            var raw = tenantVal.ToString();
            if (IsValidTenantIdentifier(raw))
            {
                return raw;
            }
        }

        return null;
    }

    private static bool IsValidTenantIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
            {
                return false;
            }
        }

        return true;
    }
}
