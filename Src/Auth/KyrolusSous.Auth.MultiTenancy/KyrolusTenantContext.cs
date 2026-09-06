namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Default implementation of the scoped ambient tenant context.
/// </summary>
public sealed class KyrolusTenantContext : IKyrolusTenantContext
{
    /// <inheritdoc />
    public string? TenantId { get; set; }

    /// <inheritdoc />
    public string? TenantName { get; set; }
}
