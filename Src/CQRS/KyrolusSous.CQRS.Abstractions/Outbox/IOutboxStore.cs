namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Abstraction for storing, retrieving, and updating transactional outbox messages.
/// </summary>
public interface IKyrolusOutboxStore
{
    /// <summary>
    /// Saves a new outbox message into the persistent store.
    /// </summary>
    Task SaveAsync(KyrolusOutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a batch of pending outbox messages ready for processing.
    /// </summary>
    /// <remarks>
    /// Implementations clamp <paramref name="batchSize"/> to <c>[1, <see cref="KyrolusOutboxLimits.MaxBatchSize"/>]</c>
    /// rather than trusting it verbatim - an unbounded caller-supplied batch size could otherwise pull
    /// an unbounded number of rows into memory in one call.
    /// <para>
    /// A <see cref="KyrolusOutboxMessageStatus.Pending"/> message is always eligible. A
    /// <see cref="KyrolusOutboxMessageStatus.Failed"/> one is only eligible once its
    /// <see cref="IKyrolusOutboxMessage.NextRetryAtUtc"/> backoff window (if any) has elapsed - without
    /// this, a message that just failed would be picked straight back up on the very next processing
    /// pass, retrying against a still-failing dependency at full frequency instead of backing off.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<KyrolusOutboxMessage>> GetPendingAsync(int batchSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a message for processing, transitioning it from <see cref="KyrolusOutboxMessageStatus.Pending"/>
    /// (or a retryable <see cref="KyrolusOutboxMessageStatus.Failed"/>, subject to the same
    /// <see cref="IKyrolusOutboxMessage.NextRetryAtUtc"/> backoff window <see cref="GetPendingAsync"/>
    /// applies) to <see cref="KyrolusOutboxMessageStatus.Processing"/>.
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
    /// Marks an outbox message as failed with error details and increments retry count. Once the
    /// retry count reaches <see cref="KyrolusOutboxLimits.MaxRetryCount"/>, the message transitions
    /// to <see cref="KyrolusOutboxMessageStatus.DeadLettered"/> instead of
    /// <see cref="KyrolusOutboxMessageStatus.Failed"/> and stops being picked up for retry.
    /// </summary>
    /// <remarks>
    /// When the message transitions to (or remains) <see cref="KyrolusOutboxMessageStatus.Failed"/>
    /// (not dead-lettered), <see cref="IKyrolusOutboxMessage.NextRetryAtUtc"/> is set to an exponential
    /// backoff computed from the new retry count (see <see cref="KyrolusOutboxLimits.BackoffBaseSeconds"/>/
    /// <see cref="KyrolusOutboxLimits.MaxBackoffDelay"/>), so <see cref="GetPendingAsync"/>/
    /// <see cref="TryClaimAsync"/> exclude it until that window elapses.
    /// </remarks>
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a batch of permanently-failed (<see cref="KyrolusOutboxMessageStatus.DeadLettered"/>)
    /// outbox messages for an operator or monitoring job to inspect.
    /// </summary>
    /// <remarks>
    /// Implementations clamp <paramref name="batchSize"/> to <c>[1, <see cref="KyrolusOutboxLimits.MaxBatchSize"/>]</c>,
    /// the same as <see cref="GetPendingAsync"/>.
    /// </remarks>
    Task<IReadOnlyList<KyrolusOutboxMessage>> GetDeadLetteredAsync(int batchSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a dead-lettered message back to <see cref="KyrolusOutboxMessageStatus.Pending"/> with
    /// its retry count reset to zero, for manual recovery after the underlying cause of failure has
    /// been fixed.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this call performed the requeue; <see langword="false"/> if the
    /// message does not exist or is not currently <see cref="KyrolusOutboxMessageStatus.DeadLettered"/>
    /// (already requeued by someone else, still retrying, or already processed) - the caller must not
    /// assume the message was reset.
    /// </returns>
    /// <remarks>
    /// A compare-and-swap on <see cref="KyrolusOutboxMessageStatus.DeadLettered"/>, not an
    /// unconditional overwrite - mirrors <see cref="TryClaimAsync"/>'s discipline so two concurrent
    /// recovery attempts (an operator's script and a monitoring job, or a double click) cannot both
    /// "succeed" against the same message.
    /// </remarks>
    Task<bool> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default);
}
