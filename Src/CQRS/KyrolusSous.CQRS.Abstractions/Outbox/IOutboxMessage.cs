namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Status of an outbox message in the transactional outbox pipeline.
/// </summary>
public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3
}

/// <summary>
/// Contract representing an outbox message destined for asynchronous publication.
/// </summary>
public interface IOutboxMessage
{
    Guid Id { get; }
    string? CorrelationId { get; }
    string EventType { get; }
    string Payload { get; }
    DateTimeOffset OccurredOnUtc { get; }
    DateTimeOffset? ProcessedOnUtc { get; }
    string? Error { get; }
    int RetryCount { get; }
    OutboxMessageStatus Status { get; }
}
