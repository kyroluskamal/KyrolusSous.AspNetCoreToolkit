namespace KyrolusSous.RabbitMQ.Abstractions.Inbox;

/// <summary>
/// Storage-agnostic abstraction for tracking and idempotently deduplicating consumed inbox messages.
/// </summary>
public interface IKyrolusInboxStore
{
    /// <summary>
    /// Checks if a message has already been processed by the given consumer.
    /// </summary>
    Task<bool> HasBeenProcessedAsync(string messageId, string consumerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a message as successfully processed by the given consumer with an optional TTL expiration.
    /// </summary>
    Task MarkAsProcessedAsync(string messageId, string consumerName, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
}
