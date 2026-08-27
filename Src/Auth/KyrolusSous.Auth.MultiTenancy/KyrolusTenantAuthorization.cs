using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.MultiTenancy;

public sealed class KyrolusTenantEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var tenantContext = httpContext.RequestServices.GetRequiredService<IKyrolusTenantContext>();

        if (!tenantContext.HasTenant)
        {
            return Results.BadRequest("Tenant identifier is missing or could not be determined.");
        }

        var user = httpContext.User;
        if (user.Identity is { IsAuthenticated: true })
        {
            // Allow SuperAdmin to bypass tenant boundary if configured
            if (user.IsInRole("SuperAdmin"))
            {
                return await next(context);
            }

            var userTenantId = user.FindFirst("tenant_id")?.Value
                            ?? user.FindFirst("tenant")?.Value;

            if (string.IsNullOrWhiteSpace(userTenantId) ||
                !string.Equals(userTenantId, tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Forbid();
            }
        }

        return await next(context);
    }
}

public static class MultiTenancyEndpointExtensions
{
    /// <summary>
    /// Enforces that the endpoint must be called within a valid tenant context and the user belongs to the tenant.
    /// </summary>
    public static RouteHandlerBuilder RequireTenant(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(new KyrolusTenantEndpointFilter());
    }
}
