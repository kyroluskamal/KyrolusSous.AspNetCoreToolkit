namespace KyrolusSous.Repositories.Marten.Abstractions.Metadata;

/// <summary>
/// Provides ambient tracing, correlation, user, and tenant metadata to enrich Marten event streams.
/// </summary>
public interface IKyrolusMartenEventMetadataProvider
{
    /// <summary>
    /// Returns the key-value dictionary of metadata headers to attach to the event stream.
    /// </summary>
    IReadOnlyDictionary<string, object> GetMetadata();
}

/// <summary>
/// Ambient correlation metadata holder.
/// </summary>
public sealed class KyrolusMartenEventMetadataContext
{
    /// <summary>
    /// Gets or sets the Correlation ID across distributed operations.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the Causation ID that triggered the current operation.
    /// </summary>
    public string? CausationId { get; set; }

    /// <summary>
    /// Gets or sets the User ID who initiated the event.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the Tenant ID.
    /// </summary>
    public string? TenantId { get; set; }
}
