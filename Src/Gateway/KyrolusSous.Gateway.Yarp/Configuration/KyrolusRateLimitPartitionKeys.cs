namespace KyrolusSous.Gateway.Yarp.Configuration;

/// <summary>
/// Utility helper providing standardized partition key resolvers for ASP.NET Core RateLimiter policies.
/// Facilitates client partitioning by IP, Tenant, or Authenticated User in enterprise multi-tenant reverse proxies.
/// </summary>
public static class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a partition key based on the client's remote IP address.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The remote IP string, or <c>"unknown_ip"</c> if unavailable.</returns>
    public static string GetClientIpKey(HttpContext context) => GetClientIpKey(context, null);

    /// <summary>
    /// Resolves a partition key based on the client's remote IP address or a trusted forwarded header (e.g. <c>"CF-Connecting-IP"</c> or <c>"X-Forwarded-For"</c>).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="forwardedHeader">Optional custom header name to inspect for client IP before falling back to connection remote IP.</param>
    /// <returns>The remote IP string, or <c>"unknown_ip"</c> if unavailable.</returns>
    public static string GetClientIpKey(HttpContext context, string? forwardedHeader)
        => GetClientIpKey(context, forwardedHeader, isTrustedProxy: null);

    /// <summary>
    /// Resolves a partition key based on the client's remote IP address or a trusted forwarded header (e.g. <c>"CF-Connecting-IP"</c> or <c>"X-Forwarded-For"</c>),
    /// strictly requiring the direct connection remote IP to satisfy the <paramref name="isTrustedProxy"/> predicate before honoring the header.
    /// Defends against rate limiter bypass via client IP spoofing (CWE-345 / CWE-290).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="forwardedHeader">Optional custom header name to inspect for client IP.</param>
    /// <param name="isTrustedProxy">Optional predicate validating whether the immediate connecting remote IP is an authorized reverse proxy / CDN.</param>
    /// <returns>The resolved client IP string, or <c>"unknown_ip"</c> if unavailable.</returns>
    public static string GetClientIpKey(HttpContext context, string? forwardedHeader, Func<System.Net.IPAddress, bool>? isTrustedProxy)
    {
        ArgumentNullException.ThrowIfNull(context);

        var remoteIp = context.Connection.RemoteIpAddress;
        var isProxyTrusted = isTrustedProxy is null || (remoteIp is not null && isTrustedProxy(remoteIp));

        if (isProxyTrusted &&
            !string.IsNullOrWhiteSpace(forwardedHeader) &&
            context.Request.Headers.TryGetValue(forwardedHeader, out var val) &&
            !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString();
            var commaIdx = raw.IndexOf(',');
            var ipStr = commaIdx >= 0 ? raw[..commaIdx].Trim() : raw.Trim();
            if (!string.IsNullOrWhiteSpace(ipStr))
            {
                return ipStr;
            }
        }

        return remoteIp?.ToString() ?? "unknown_ip";
    }

    /// <summary>
    /// Resolves a partition key based on the ambient multi-tenant identity.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The resolved tenant identifier, or <c>"anonymous_tenant"</c> if none is resolved.</returns>
    public static string GetTenantKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenantContext = context.RequestServices?.GetService<IKyrolusTenantContext>();
        if (tenantContext is { HasTenant: true } && !string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return tenantContext.TenantId;
        }

        return "anonymous_tenant";
    }

    /// <summary>
    /// Resolves a combined partition key incorporating both Tenant ID and Client IP for fair-share multi-tenant resource throttling.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>A compound key formatted as <c>"{tenant}:{ip}"</c>.</returns>
    public static string GetTenantAndIpKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"{GetTenantKey(context)}:{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on the authenticated user identifier (Name, NameIdentifier, or sub claim),
    /// falling back to the client IP address for unauthenticated requests.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The authenticated user key or IP fallback.</returns>
    public static string GetUserKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User?.Identity is { IsAuthenticated: true })
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? context.User.FindFirst("sub")?.Value
                      ?? context.User.Identity.Name;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return $"user_{userId}";
            }
        }

        return $"ip_{GetClientIpKey(context)}";
    }
}
