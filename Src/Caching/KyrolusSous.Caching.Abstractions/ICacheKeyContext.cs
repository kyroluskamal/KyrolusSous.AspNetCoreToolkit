namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Defines the contextual scope information used by <see cref="IKyrolusCacheKeyFactory"/> 
/// to isolate and partition cache entries across tenants, branches, and regions.
/// </summary>
public interface IKyrolusCacheKeyContext
{
    /// <summary>
    /// Gets a stable composite scope key used for cache isolation (e.g., <c>"tenant=corp1;branch=cairo"</c>).
    /// </summary>
    string? ScopeKey { get; }

    /// <summary>
    /// Gets the optional logical cache region (e.g., <c>"catalog"</c>, <c>"identity"</c>, or <c>"pricing"</c>).
    /// </summary>
    string? Region => null;

    /// <summary>
    /// Gets the optional unique tenant identifier used for multi-tenant data partitioning.
    /// </summary>
    string? TenantId => null;
}
