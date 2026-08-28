using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Abstractions.Interfaces
{
    /// <summary>
    /// Core utility interface for setting up queues and publishing messages with RabbitMQ.
    /// </summary>
    public interface IKyrolusRabbitMQUtils
    {
        Task SetupQueueAsync(string exchange, IKyrolusQueueSetup[] queues, string type = ExchangeType.Direct, bool isDurable = true, bool autoDelete = false, IDictionary<string, object?>? arguments = null);
        Task PublishAsync<TEvent>(string exchange, string routingKey, TEvent body, bool mandatory = true, BasicProperties? basicProperties = null);
        Task PublishAsync<TEvent>(string exchange, string routingKey, TEvent body, string? correlationId, IDictionary<string, object?>? headers = null, CancellationToken cancellationToken = default);
        Task PublishBatchAsync<TEvent>(string exchange, string routingKey, IEnumerable<TEvent> events, bool waitForConfirms = true, CancellationToken cancellationToken = default);
        Task PublishDelayedAsync<TEvent>(string exchange, string routingKey, TEvent body, TimeSpan delay, string? correlationId = null, IDictionary<string, object?>? headers = null, CancellationToken cancellationToken = default);
    }
}

namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMQUtils"/>.
    /// </summary>
    public interface IKyrolusRabbitMQUtils : global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMQUtils
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMQUtils"/>.
    /// </summary>
    public interface IRabbitMQUtils : IKyrolusRabbitMQUtils
    {
    }
}
