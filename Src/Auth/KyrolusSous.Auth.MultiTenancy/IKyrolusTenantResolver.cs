using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Scoped ambient context representing the tenant of the current HTTP request.
/// </summary>
public interface IKyrolusTenantContext
{
    string? TenantId { get; set; }
    string? TenantName { get; set; }
    bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
}

public sealed class KyrolusTenantContext : IKyrolusTenantContext
{
    public string? TenantId { get; set; }
    public string? TenantName { get; set; }
}

/// <summary>
/// Strategy contract for extracting the tenant identifier from an incoming HTTP request.
/// </summary>
public interface IKyrolusTenantResolver
{
    ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext);
}

/// <summary>
/// Resolves tenant ID from a custom HTTP header (default: <c>X-Tenant-Id</c>).
/// </summary>
public sealed class KyrolusHeaderTenantResolver(string headerName = "X-Tenant-Id") : IKyrolusTenantResolver
{
    private readonly string _effectiveHeader = string.IsNullOrWhiteSpace(headerName) ? "X-Tenant-Id" : headerName.Trim();

    public ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(_effectiveHeader, out var values))
        {
            var value = values.ToString().Trim();
            if (!string.IsNullOrEmpty(value) && value.Length <= 64 &&
                value.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '-' or '_' or '.'))
            {
                return ValueTask.FromResult<string?>(value);
            }
        }

        return ValueTask.FromResult<string?>(null);
    }
}

/// <summary>
/// Resolves tenant ID from the authenticated user's claims (default: <c>tenant_id</c>).
/// </summary>
public sealed class KyrolusClaimTenantResolver(string claimType = "tenant_id") : IKyrolusTenantResolver
{
    public ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
    {
        var rawClaim = httpContext.User.FindFirst(claimType)?.Value;
        if (!string.IsNullOrWhiteSpace(rawClaim))
        {
            var trimmed = rawClaim.Trim();
            if (trimmed.Length <= 64 &&
                trimmed.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '-' or '_' or '.'))
            {
                return ValueTask.FromResult<string?>(trimmed);
            }
        }

        return ValueTask.FromResult<string?>(null);
    }
}

/// <summary>
/// Resolves tenant ID from the first subdomain segment (e.g. <c>acme.api.example.com</c> -> <c>acme</c>).
/// </summary>
public sealed class KyrolusSubdomainTenantResolver : IKyrolusTenantResolver
{
    public ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
    {
        var host = httpContext.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host) ||
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            System.Net.IPAddress.TryParse(host, out _))
        {
            return ValueTask.FromResult<string?>(null);
        }

        var parts = host.Split('.');
        if (parts.Length >= 3 && !string.Equals(parts[0], "www", StringComparison.OrdinalIgnoreCase))
        {
            var subdomain = parts[0].Trim();
            if (subdomain.Length <= 64 &&
                subdomain.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '-' or '_' or '.'))
            {
                return ValueTask.FromResult<string?>(subdomain);
            }
        }

        return ValueTask.FromResult<string?>(null);
    }
}
