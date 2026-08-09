using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.EF.Config;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class KyrolusHttpEndpointContext(
    IHttpContextAccessor accessor,
    IOptions<KyrolusEndpointKitOptions> options) : IKyrolusEndpointContext
{
    private readonly IHttpContextAccessor accessor = accessor;
    private readonly KyrolusEndpointKitOptions options = options.Value;

    public string? TenantId => ResolveValue(options.TenantHeaderName, options.TenantClaimType);
    public string? ScopeKey => ResolveValue(options.ScopeHeaderName, options.ScopeClaimType);

    private string? ResolveValue(string? headerName, string? claimType)
    {
        var context = accessor.HttpContext;
        if (context is null) return null;

        if (!string.IsNullOrWhiteSpace(headerName)
            && context.Request.Headers.TryGetValue(headerName, out var header))
        {
            var value = header.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        if (!string.IsNullOrWhiteSpace(claimType))
        {
            return context.User.FindFirstValue(claimType);
        }

        return null;
    }
}
