namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Status of an outbox message in the transactional outbox pipeline.
/// </summary>
public enum KyrolusOutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3,

    /// <summary>
    /// The message has failed <see cref="KyrolusOutboxLimits.MaxRetryCount"/> or more consecutive
    /// times and will no longer be picked up for retry. A distinct terminal state - rather than
    /// leaving it as <see cref="Failed"/> forever - makes it discoverable to an operator (via
    /// <see cref="IKyrolusOutboxStore.GetDeadLetteredAsync"/>) instead of sitting silently
    /// indistinguishable from a message that failed once and will succeed on the next pass.
    /// </summary>
    DeadLettered = 4
}

/// <summary>
/// Contract representing an outbox message destined for asynchronous publication.
/// </summary>
public interface IKyrolusOutboxMessage
{
    Guid Id { get; }
    string? CorrelationId { get; }
    string EventType { get; }
    string Payload { get; }
    DateTimeOffset OccurredOnUtc { get; }
    DateTimeOffset? ProcessedOnUtc { get; }
    string? Error { get; }
    int RetryCount { get; }
    KyrolusOutboxMessageStatus Status { get; }

    /// <summary>
    /// For a <see cref="KyrolusOutboxMessageStatus.Failed"/> message, the earliest time it becomes
    /// claimable again - <see langword="null"/> for a message that has never failed, or once it has
    /// transitioned to <see cref="KyrolusOutboxMessageStatus.DeadLettered"/> (there is no "next retry"
    /// once retries have stopped altogether). See <see cref="KyrolusOutboxLimits.BackoffBaseSeconds"/>
    /// for how this is computed.
    /// </summary>
    DateTimeOffset? NextRetryAtUtc { get; }
}
