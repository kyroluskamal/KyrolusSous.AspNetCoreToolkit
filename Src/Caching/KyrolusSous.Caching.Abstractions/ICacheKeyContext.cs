namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Defines the contextual scope information used by <see cref="IKyrolusCacheKeyFactory"/> 
/// to isolate and partition cache entries across tenants, branches, and regions.
/// </summary>
/// <remarks>
/// In multi-tenant or multi-branch applications, storing cache keys without contextual isolation
/// leads to data leaks (Tenant B seeing Tenant A's cached products). By implementing this interface,
/// the cache subsystem automatically prefixes keys with the active tenant or scope.
/// </remarks>
/// <example>
/// Example implementation in an ASP.NET Core request:
/// <code>
/// public class HttpCacheKeyContext(IHttpContextAccessor accessor) : ICacheKeyContext
/// {
///     public string? TenantId => accessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
///     public string? ScopeKey => TenantId is not null ? $"tenant:{TenantId}" : null;
///     public string? Region => "catalog";
/// }
/// </code>
/// </example>
public interface ICacheKeyContext
{
    /// <summary>
    /// Gets a stable composite scope key used for cache isolation (e.g., <c>"tenant=corp1;branch=cairo"</c>).
    /// </summary>
    /// <value>A string representing the current scope, or <c>null</c>/empty for the global application scope.</value>
    string? ScopeKey { get; }

    /// <summary>
    /// Gets the optional logical cache region (e.g., <c>"catalog"</c>, <c>"identity"</c>, or <c>"pricing"</c>).
    /// </summary>
    /// <value>The cache region name, or <c>null</c> if no specific region is targeted.</value>
    string? Region => null;

    /// <summary>
    /// Gets the optional unique tenant identifier used for multi-tenant data partitioning.
    /// </summary>
    /// <value>The unique ID of the tenant owning the current execution context, or <c>null</c> in single-tenant mode.</value>
    string? TenantId => null;
}
