using RabbitMQ.Client;

namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

/// <summary>
/// Core utility interface for setting up queues and publishing messages with RabbitMQ.
/// </summary>
public interface IKyrolusRabbitMQUtils
{
    Task SetupQueueAsync(string exchange, IQueueSetup[] queues, string type = ExchangeType.Direct, bool isDurable = true, bool autoDelete = false, IDictionary<string, object?>? arguments = null);
    Task PublishAsync<TEvent>(string exchange, string routingKey, TEvent body, bool mandatory = true, BasicProperties? basicProperties = null);
    Task PublishAsync<TEvent>(string exchange, string routingKey, TEvent body, string? correlationId, IDictionary<string, object?>? headers = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Backward-compatibility alias for <see cref="IKyrolusRabbitMQUtils"/>.
/// </summary>
public interface IRabbitMQUtils : IKyrolusRabbitMQUtils
{
}
