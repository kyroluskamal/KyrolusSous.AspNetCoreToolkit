namespace KyrolusSous.Repositories.Marten.Abstractions.MultiTenancy;

/// <summary>
/// Provides ambient tenant context for Marten document store and event streams.
/// </summary>
public interface IKyrolusMartenTenantContext
{
    /// <summary>
    /// Gets the current ambient Tenant ID.
    /// </summary>
    string? TenantId { get; }
}
