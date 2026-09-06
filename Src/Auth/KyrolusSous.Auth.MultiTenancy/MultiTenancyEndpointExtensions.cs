using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Extension methods for securing ASP.NET Core endpoints with tenant isolation constraints.
/// </summary>
public static class MultiTenancyEndpointExtensions
{
    /// <summary>
    /// Enforces that the endpoint must be called within a valid tenant context and the user belongs to the tenant.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder with the tenant filter applied.</returns>
    public static RouteHandlerBuilder RequireTenant(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(new KyrolusTenantEndpointFilter());
    }
}
