namespace KyrolusSous.RabbitMQ.Abstractions.Outbox;

/// <summary>
/// Defines a message stored in the transactional outbox.
/// </summary>
public interface IKyrolusOutboxMessage
{
    string Id { get; set; }
    string Exchange { get; set; }
    string RoutingKey { get; set; }
    string MessageType { get; set; }
    string Payload { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? ProcessedAt { get; set; }
    int RetryCount { get; set; }
    string? Error { get; set; }
    Dictionary<string, string> Headers { get; set; }
}

/// <summary>
/// Default implementation of <see cref="IKyrolusOutboxMessage"/>.
/// </summary>
public class KyrolusOutboxMessage : IKyrolusOutboxMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Exchange { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string> Headers { get; set; } = [];
}

/// <summary>
/// Storage-agnostic abstraction for persisting and reading transactional outbox messages.
/// </summary>
public interface IKyrolusOutboxStore
{
    Task AddAsync(IKyrolusOutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IKyrolusOutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default);
}
