using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Resolves tenant ID from the authenticated user's claims (default: <c>tenant_id</c>).
/// </summary>
public sealed class KyrolusClaimTenantResolver(string claimType = "tenant_id") : IKyrolusTenantResolver
{
    /// <inheritdoc />
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
