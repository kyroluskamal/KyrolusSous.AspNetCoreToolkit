namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Default entity model representing a transactional outbox message.
/// </summary>
public sealed class KyrolusOutboxMessage : IOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? CorrelationId { get; set; }
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset OccurredOnUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
}
