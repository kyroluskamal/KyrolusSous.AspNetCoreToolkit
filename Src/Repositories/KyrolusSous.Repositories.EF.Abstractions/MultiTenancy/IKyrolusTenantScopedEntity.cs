namespace KyrolusSous.Repositories.EF.Abstractions.MultiTenancy;

/// <summary>
/// Marks an entity as belonging to a specific tenant for strict multi-tenant isolation.
/// </summary>
public interface IKyrolusTenantScopedEntity
{
    /// <summary>
    /// Gets or sets the unique tenant identifier.
    /// </summary>
    string TenantId { get; set; }
}

/// <summary>
/// Provides ambient context for resolving the current tenant identifier.
/// </summary>
public interface IKyrolusCurrentTenantContext
{
    /// <summary>
    /// Gets the current tenant identifier (or <c>null</c> if host/cross-tenant context).
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets whether a valid tenant context is resolved.
    /// </summary>
    bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
}
