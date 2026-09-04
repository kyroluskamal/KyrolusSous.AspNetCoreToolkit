namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Default entity model representing a transactional outbox message.
/// </summary>
public sealed class KyrolusOutboxMessage : IKyrolusOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? CorrelationId { get; set; }
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset OccurredOnUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public KyrolusOutboxMessageStatus Status { get; set; } = KyrolusOutboxMessageStatus.Pending;
}
