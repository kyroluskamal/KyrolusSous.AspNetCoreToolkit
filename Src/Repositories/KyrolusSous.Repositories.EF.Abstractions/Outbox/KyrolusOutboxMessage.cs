namespace KyrolusSous.Repositories.EF.Abstractions.Outbox;

/// <summary>
/// Represents a domain integration event stored in the outbox table within the same database transaction.
/// </summary>
public sealed class KyrolusOutboxMessage
{
    /// <summary>
    /// Gets or sets the unique message identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the fully-qualified event or message type name.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized JSON payload of the event.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the event occurred.
    /// </summary>
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp when the message was dispatched to the broker (or <c>null</c> if pending).
    /// </summary>
    public DateTime? ProcessedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets any error message encountered during publishing.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }
}

/// <summary>
/// Provides operations for storing and reading outbox messages.
/// </summary>
public interface IKyrolusOutboxStore
{
    /// <summary>
    /// Enqueues an outbox message to be committed within the active unit of work transaction.
    /// </summary>
    Task EnqueueAsync(KyrolusOutboxMessage message, CancellationToken cancellationToken = default);
}
