namespace KyrolusSous.RabbitMQ.Abstractions.Dlq;

/// <summary>
/// Abstraction for managing, inspecting, and replaying dead-lettered messages in RabbitMQ.
/// </summary>
public interface IKyrolusDlqManager
{
    /// <summary>
    /// Returns the number of messages currently residing in the dead letter queue.
    /// </summary>
    Task<uint> GetDeadLetterMessageCountAsync(string dlqName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays (re-queues) messages from the dead letter queue back to a target exchange and routing key.
    /// </summary>
    /// <returns>The number of messages successfully replayed.</returns>
    Task<int> ReplayDeadLetterMessagesAsync(
        string dlqName,
        string targetExchange,
        string targetRoutingKey,
        int maxMessages = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges all messages from the specified dead letter queue.
    /// </summary>
    Task PurgeDeadLetterQueueAsync(string dlqName, CancellationToken cancellationToken = default);
}
