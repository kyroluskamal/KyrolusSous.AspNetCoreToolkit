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
    /// Atomically claims a message for processing, transitioning it from <see cref="KyrolusOutboxMessageStatus.Pending"/>
    /// (or a retryable <see cref="KyrolusOutboxMessageStatus.Failed"/>) to <see cref="KyrolusOutboxMessageStatus.Processing"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this call won the claim and the caller should process the message;
    /// <see langword="false"/> if the message no longer exists or was already claimed/processed by
    /// someone else (a concurrent processor pass, another application instance) - the caller must skip
    /// it rather than process it again.
    /// </returns>
    /// <remarks>
    /// <see cref="GetPendingAsync"/> alone only reads candidates - without a separate claim step,
    /// two overlapping processing passes (a slow run still in flight when the next timer tick fires, or
    /// two application instances against a shared store) can both read the same message and publish it
    /// twice.
    /// </remarks>
    Task<bool> TryClaimAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox message as successfully processed.
    /// </summary>
    Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox message as failed with error details and increments retry count.
    /// </summary>
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
}
