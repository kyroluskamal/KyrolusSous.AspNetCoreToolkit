namespace KyrolusSous.Repositories.Marten.Abstractions.Outbox;

/// <summary>
/// Represents a transactional outbox message to be persisted in Marten PostgreSQL document store.
/// </summary>
public sealed class KyrolusMartenOutboxMessage
{
    /// <summary>
    /// Gets or sets the unique message identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the event or message CLR type name.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the event occurred in UTC.
    /// </summary>
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether the outbox message has been processed/dispatched.
    /// </summary>
    public bool Processed { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was processed in UTC.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }

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
/// Defines the contract for enqueuing and retrieving Marten transactional outbox messages.
/// </summary>
public interface IKyrolusMartenOutboxStore
{
    /// <summary>
    /// Enqueues a new outbox message within the active Marten document session.
    /// </summary>
    Task EnqueueAsync(KyrolusMartenOutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pending uncommitted outbox messages for dispatching.
    /// </summary>
    Task<IReadOnlyList<KyrolusMartenOutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox message as successfully processed.
    /// </summary>
    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
}
