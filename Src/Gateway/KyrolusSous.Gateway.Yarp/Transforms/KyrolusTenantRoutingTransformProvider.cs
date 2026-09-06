namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that enforces multi-tenant boundary isolation and defends against tenant spoofing attacks.
/// Resolves tenant identity using <see cref="IKyrolusTenantResolver"/> and securely injects the validated <c>X-Tenant-ID</c>
/// into proxied upstream requests while stripping untrusted client-supplied tenant headers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant Spoofing Defense:</b><br/>
/// External callers cannot bypass tenant boundaries by injecting arbitrary <c>X-Tenant-ID</c> headers.
/// The gateway strips unverified client tenant headers from the proxy request and injects only the tenant verified
/// by authenticated JWT claims or authoritative domain routing.
/// </para>
/// </remarks>
public sealed class KyrolusTenantRoutingTransformProvider : ITransformProvider
{
    private static readonly byte[] TenantRequiredResponseBytes =
        """{"title":"Unauthorized","status":401,"detail":"A valid tenant context is required to access this route."}"""u8.ToArray();

    private static readonly IKyrolusTenantResolver DefaultFallbackResolver = new KyrolusCompositeTenantResolver(
    [
        new KyrolusClaimTenantResolver(),
        new KyrolusSubdomainTenantResolver()
    ]);

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
        var routeMetadata = context.Route?.Metadata;
        var requireTenant = routeMetadata != null &&
                            routeMetadata.TryGetValue("Kyrolus:Tenant:Required", out var req) &&
                            bool.TryParse(req, out var isReq) && isReq;

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var httpContext = transformContext.HttpContext;

            // 1. Defend against tenant spoofing: strip any untrusted incoming tenant headers
            transformContext.ProxyRequest.Headers.Remove("X-Tenant-ID");
            transformContext.ProxyRequest.Headers.Remove("X-Tenant-Id");

            // 2. Resolve verified tenant from ambient context or resolver
            string? resolvedTenant = null;
            var tenantContext = httpContext.RequestServices?.GetService<IKyrolusTenantContext>();
            if (tenantContext is { HasTenant: true })
            {
                resolvedTenant = tenantContext.TenantId;
            }
            else
            {
                var resolver = httpContext.RequestServices?.GetService<IKyrolusTenantResolver>()
                            ?? DefaultFallbackResolver;

                resolvedTenant = await resolver.ResolveTenantIdAsync(httpContext);
            }

            // 3. Enforce tenant presence if this route strictly requires a tenant
            if (requireTenant && (string.IsNullOrWhiteSpace(resolvedTenant) || !IsValidTenantIdentifier(resolvedTenant)))
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                httpContext.Response.ContentType = "application/problem+json";
                await httpContext.Response.Body.WriteAsync(TenantRequiredResponseBytes, httpContext.RequestAborted);
                return;
            }

            // 4. Inject the authoritative, sanitized tenant ID into upstream request headers
            if (!string.IsNullOrWhiteSpace(resolvedTenant) && IsValidTenantIdentifier(resolvedTenant))
            {
                transformContext.ProxyRequest.Headers.Add("X-Tenant-ID", resolvedTenant);
            }
        });
    }

    private static bool IsValidTenantIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
            {
                return false;
            }
        }

        return true;
    }
}
