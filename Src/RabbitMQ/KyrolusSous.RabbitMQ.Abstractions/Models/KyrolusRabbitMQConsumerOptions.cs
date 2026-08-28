namespace KyrolusSous.RabbitMQ.Abstractions.Models;

/// <summary>
/// Options for configuring a RabbitMQ message consumer.
/// </summary>
public sealed class KyrolusRabbitMQConsumerOptions
{
    public string QueueName { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public bool Durable { get; set; } = true;
    public bool Exclusive { get; set; } = false;
    public bool AutoDelete { get; set; } = false;
    public bool AutoAck { get; set; } = false;
    public ushort PrefetchCount { get; set; } = 10;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public bool UseDeadLetterOnFailure { get; set; } = true;
    public IDictionary<string, object?>? Arguments { get; set; }
}

/// <summary>
/// Delivery context for a consumed message.
/// </summary>
public sealed record KyrolusRabbitMQConsumeContext(
    string Exchange,
    string RoutingKey,
    ulong DeliveryTag,
    bool Redelivered,
    string? MessageId,
    string? CorrelationId,
    string? TraceParent,
    IDictionary<string, object?> Headers);
