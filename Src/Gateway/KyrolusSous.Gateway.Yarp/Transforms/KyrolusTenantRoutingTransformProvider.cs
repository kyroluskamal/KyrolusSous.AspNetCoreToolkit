using System.Net;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Resolves tenant information with multi-source fallback (Request Header -> Subdomain)
/// and injects the validated X-Tenant-ID header into the proxied backend request.
/// Safely ignores IP addresses, localhost, and reserved subdomains (api, www, admin, app, etc.).
/// </summary>
public sealed class KyrolusTenantRoutingTransformProvider : ITransformProvider
{
    private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "app", "mail", "staging", "dev", "test", "gateway", "proxy"
    };

    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

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
