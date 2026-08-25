namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Abstraction for storing, retrieving, and updating transactional outbox messages.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Saves a new outbox message into the persistent store.
    /// </summary>
    Task SaveAsync(KyrolusOutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a batch of pending outbox messages ready for processing.
    /// </summary>
    Task<IReadOnlyList<KyrolusOutboxMessage>> GetPendingAsync(int batchSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox message as successfully processed.
    /// </summary>
    Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox message as failed with error details and increments retry count.
    /// </summary>
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
}
