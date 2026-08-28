using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using System.Security.Claims;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Services;

public sealed class HttpTenantResolver(IHttpContextAccessor accessor) : IKyrolusTenantResolver
{
    private readonly IHttpContextAccessor accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));

    public string? ResolveTenantId()
    {
        var context = accessor.HttpContext;
        if (context is null) return null;

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var header) && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        var claim = context.User.FindFirstValue("tenant_id");
        return string.IsNullOrWhiteSpace(claim) ? null : claim;
    }
}
