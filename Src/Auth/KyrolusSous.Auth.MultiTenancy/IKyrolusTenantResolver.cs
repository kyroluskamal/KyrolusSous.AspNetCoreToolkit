using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Strategy contract for extracting the tenant identifier from an incoming HTTP request.
/// </summary>
public interface IKyrolusTenantResolver
{
    /// <summary>
    /// Asynchronously resolves the tenant identifier from the provided HTTP context.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The resolved tenant ID string, or <c>null</c> if not determined.</returns>
    ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext);
}
