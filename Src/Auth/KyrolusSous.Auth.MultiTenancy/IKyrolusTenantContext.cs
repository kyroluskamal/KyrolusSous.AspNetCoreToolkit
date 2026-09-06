namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Scoped ambient context representing the tenant of the current HTTP request.
/// </summary>
public interface IKyrolusTenantContext
{
    /// <summary>
    /// Gets or sets the unique tenant identifier.
    /// </summary>
    string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the optional friendly display name of the tenant.
    /// </summary>
    string? TenantName { get; set; }

    /// <summary>
    /// Gets a value indicating whether a valid tenant identifier is present.
    /// </summary>
    bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
}
