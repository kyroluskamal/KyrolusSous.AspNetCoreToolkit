using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Abstractions.Topology;

/// <summary>
/// Definition of a RabbitMQ exchange topology.
/// </summary>
public sealed record KyrolusExchangeDefinition(
    string Name,
    string Type = ExchangeType.Direct,
    bool Durable = true,
    bool AutoDelete = false,
    IDictionary<string, object?>? Arguments = null);

/// <summary>
/// Definition of a RabbitMQ queue topology.
/// </summary>
public sealed record KyrolusQueueDefinition(
    string Name,
    bool Durable = true,
    bool Exclusive = false,
    bool AutoDelete = false,
    IDictionary<string, object?>? Arguments = null);

/// <summary>
/// Definition of a binding topology between exchange and queue.
/// </summary>
public sealed record KyrolusBindingDefinition(
    string QueueName,
    string ExchangeName,
    string RoutingKey,
    IDictionary<string, object?>? Arguments = null);

/// <summary>
/// Fluent builder for declarative RabbitMQ topology configuration including Quorum, Streams, Priority, and Headers.
/// </summary>
public interface IKyrolusRabbitMQTopologyBuilder
{
    IReadOnlyList<KyrolusExchangeDefinition> Exchanges { get; }
    IReadOnlyList<KyrolusQueueDefinition> Queues { get; }
    IReadOnlyList<KyrolusBindingDefinition> Bindings { get; }

    IKyrolusRabbitMQTopologyBuilder AddExchange(
        string name,
        string type = ExchangeType.Direct,
        bool durable = true,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null);

    IKyrolusRabbitMQTopologyBuilder AddQueue(
        string name,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null);

    IKyrolusRabbitMQTopologyBuilder AddPriorityQueue(
        string name,
        byte maxPriority = 10,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null);

    IKyrolusRabbitMQTopologyBuilder AddQuorumQueue(
        string name,
        int? deliveryLimit = null,
        IDictionary<string, object?>? arguments = null);

    IKyrolusRabbitMQTopologyBuilder AddStream(
        string name,
        TimeSpan? maxAge = null,
        IDictionary<string, object?>? arguments = null);

    IKyrolusRabbitMQTopologyBuilder BindQueue(
        string queueName,
        string exchangeName,
        string routingKey,
        IDictionary<string, object?>? arguments = null);

    IKyrolusRabbitMQTopologyBuilder BindHeadersQueue(
        string queueName,
        string exchangeName,
        string xMatch,
        IDictionary<string, object?> headers);
}
