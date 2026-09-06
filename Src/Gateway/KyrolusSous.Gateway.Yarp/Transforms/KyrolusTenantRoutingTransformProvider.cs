namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that resolves multi-tenant identity using a hardened multi-source fallback strategy
/// (Explicit Request Header -&gt; Subdomain Extraction) and injects the validated <c>X-Tenant-ID</c> header into proxied requests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-Tenancy Resolution Hierarchy:</b><br/>
/// <list type="number">
/// <item><description><b>Explicit Header Priority</b>: Checks if the caller supplied <c>X-Tenant-ID</c> or <c>X-Tenant-Id</c>. If valid, this takes precedence.</description></item>
/// <item><description><b>Subdomain Fallback</b>: If no header is present, extracts the leading subdomain segment from the request host, safely ignoring IP addresses, localhost, and reserved infrastructure prefixes (<c>www</c>, <c>api</c>, <c>admin</c>, <c>app</c>, <c>staging</c>, etc.).</description></item>
/// <item><description><b>Sanitization</b>: Verifies character sets (alphanumeric, dashes, underscores, dots) and maximum length (64 chars) to prevent header injection.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class KyrolusTenantRoutingTransformProvider : ITransformProvider
{
    private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "app", "mail", "staging", "dev", "test", "gateway", "proxy"
    };

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the tenant resolution and header injection transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var httpContext = transformContext.HttpContext;
            string? resolvedTenant = null;

            // 1. First priority: Check if incoming request already contains an explicit X-Tenant-ID or X-Tenant-Id header
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-ID", out var headerVal) ||
                httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out headerVal))
            {
                var candidate = headerVal.ToString().Trim();
                if (IsValidTenantIdentifier(candidate))
                {
                    resolvedTenant = candidate;
                }
            }

            // 2. Second priority: Extract from subdomain if not an IP, not localhost, and not a reserved name
            if (resolvedTenant is null)
            {
                var host = httpContext.Request.Host.Host;
                if (!string.IsNullOrWhiteSpace(host) &&
                    !host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                    !IPAddress.TryParse(host, out _))
                {
                    var parts = host.Split('.');
                    if (parts.Length >= 3 && !ReservedSubdomains.Contains(parts[0]))
                    {
                        var candidate = parts[0].Trim();
                        if (IsValidTenantIdentifier(candidate))
                        {
                            resolvedTenant = candidate;
                        }
                    }
                }
            }

            // 3. Inject into ProxyRequest headers if resolved
            if (!string.IsNullOrWhiteSpace(resolvedTenant))
            {
                transformContext.ProxyRequest.Headers.Remove("X-Tenant-ID");
                transformContext.ProxyRequest.Headers.Add("X-Tenant-ID", resolvedTenant);
            }

            return ValueTask.CompletedTask;
        });
    }

    private static bool IsValidTenantIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 64 &&
        value.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '-' or '_' or '.');
}
