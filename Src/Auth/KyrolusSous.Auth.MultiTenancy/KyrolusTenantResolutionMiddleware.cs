using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Middleware that resolves the current request's tenant identity and populates the ambient <see cref="IKyrolusTenantContext"/>.
/// </summary>
public sealed class KyrolusTenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusTenantResolutionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the ASP.NET Core pipeline.</param>
    public KyrolusTenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the middleware to resolve and store tenant identity for the current HTTP context.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="resolver">The registered tenant resolver service.</param>
    /// <param name="tenantContext">The ambient scoped tenant context.</param>
    public async Task InvokeAsync(
        HttpContext context,
        IKyrolusTenantResolver resolver,
        IKyrolusTenantContext tenantContext)
    {
        tenantContext.TenantId = await resolver.ResolveTenantIdAsync(context);
        await _next(context);
    }
}
