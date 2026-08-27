using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

public sealed class KyrolusCompositeTenantResolver : IKyrolusTenantResolver
{
    private readonly IEnumerable<IKyrolusTenantResolver> _resolvers;

    public KyrolusCompositeTenantResolver(IEnumerable<IKyrolusTenantResolver> resolvers)
    {
        _resolvers = resolvers ?? throw new ArgumentNullException(nameof(resolvers));
    }

    public async ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
    {
        foreach (var resolver in _resolvers)
        {
            try
            {
                var tenantId = await resolver.ResolveTenantIdAsync(httpContext);
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    return tenantId;
                }
            }
            catch
            {
                // Fault tolerance: allow fallback to subsequent resolvers in the chain
            }
        }

        return null;
    }
}

public sealed class KyrolusTenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public KyrolusTenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IKyrolusTenantResolver resolver,
        IKyrolusTenantContext tenantContext)
    {
        tenantContext.TenantId = await resolver.ResolveTenantIdAsync(context);
        await _next(context);
    }
}
