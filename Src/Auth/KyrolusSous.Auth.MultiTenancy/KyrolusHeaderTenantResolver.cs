using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Resolves tenant ID from a custom HTTP header (default: <c>X-Tenant-Id</c>).
/// </summary>
public sealed class KyrolusHeaderTenantResolver(string headerName = "X-Tenant-Id") : IKyrolusTenantResolver
{
    private readonly string _effectiveHeader = string.IsNullOrWhiteSpace(headerName) ? "X-Tenant-Id" : headerName.Trim();

    /// <inheritdoc />
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
