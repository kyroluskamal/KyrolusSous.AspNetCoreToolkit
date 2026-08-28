using KyrolusSous.RabbitMQ.Abstractions.Topology;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Runtime.Topology;

/// <summary>
/// Fluent topology builder and applicator for RabbitMQ broker exchanges, queues, bindings, quorum queues, and streams.
/// </summary>
public class KyrolusRabbitMQTopologyBuilder : IKyrolusRabbitMQTopologyBuilder
{
    private readonly List<KyrolusExchangeDefinition> _exchanges = [];
    private readonly List<KyrolusQueueDefinition> _queues = [];
    private readonly List<KyrolusBindingDefinition> _bindings = [];

    public IReadOnlyList<KyrolusExchangeDefinition> Exchanges => _exchanges;
    public IReadOnlyList<KyrolusQueueDefinition> Queues => _queues;
    public IReadOnlyList<KyrolusBindingDefinition> Bindings => _bindings;

    public IKyrolusRabbitMQTopologyBuilder AddExchange(
        string name,
        string type = ExchangeType.Direct,
        bool durable = true,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _exchanges.Add(new KyrolusExchangeDefinition(name, type, durable, autoDelete, arguments));
        return this;
    }

    public IKyrolusRabbitMQTopologyBuilder AddQueue(
        string name,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _queues.Add(new KyrolusQueueDefinition(name, durable, exclusive, autoDelete, arguments));
        return this;
    }

    public IKyrolusRabbitMQTopologyBuilder AddPriorityQueue(
        string name,
        byte maxPriority = 10,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null)
    {
        var args = arguments != null ? new Dictionary<string, object?>(arguments) : new Dictionary<string, object?>();
        args["x-max-priority"] = maxPriority;

        return AddQueue(name, durable, exclusive, autoDelete, args);
    }

    public IKyrolusRabbitMQTopologyBuilder AddQuorumQueue(
        string name,
        int? deliveryLimit = null,
        IDictionary<string, object?>? arguments = null)
    {
        var args = arguments != null ? new Dictionary<string, object?>(arguments) : new Dictionary<string, object?>();
        args["x-queue-type"] = "quorum";
        if (deliveryLimit.HasValue)
        {
            args["x-delivery-limit"] = deliveryLimit.Value;
        }

        return AddQueue(name, durable: true, exclusive: false, autoDelete: false, args);
    }

    public IKyrolusRabbitMQTopologyBuilder AddStream(
        string name,
        TimeSpan? maxAge = null,
        IDictionary<string, object?>? arguments = null)
    {
        var args = arguments != null ? new Dictionary<string, object?>(arguments) : new Dictionary<string, object?>();
        args["x-queue-type"] = "stream";
        if (maxAge.HasValue)
        {
            args["x-max-age"] = $"{maxAge.Value.TotalHours:0}h";
        }

        return AddQueue(name, durable: true, exclusive: false, autoDelete: false, args);
    }

    public IKyrolusRabbitMQTopologyBuilder BindQueue(
        string queueName,
        string exchangeName,
        string routingKey,
        IDictionary<string, object?>? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        _bindings.Add(new KyrolusBindingDefinition(queueName, exchangeName, routingKey, arguments));
        return this;
    }

    public IKyrolusRabbitMQTopologyBuilder BindHeadersQueue(
        string queueName,
        string exchangeName,
        string xMatch,
        IDictionary<string, object?> headers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentNullException.ThrowIfNull(headers);

        var args = new Dictionary<string, object?>(headers)
        {
            ["x-match"] = xMatch
        };

        _bindings.Add(new KyrolusBindingDefinition(queueName, exchangeName, string.Empty, args));
        return this;
    }

    public async Task ApplyAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        foreach (var ex in _exchanges)
        {
            await channel.ExchangeDeclareAsync(
                exchange: ex.Name,
                type: ex.Type,
                durable: ex.Durable,
                autoDelete: ex.AutoDelete,
                arguments: ex.Arguments,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        foreach (var q in _queues)
        {
            await channel.QueueDeclareAsync(
                queue: q.Name,
                durable: q.Durable,
                exclusive: q.Exclusive,
                autoDelete: q.AutoDelete,
                arguments: q.Arguments,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        foreach (var b in _bindings)
        {
            await channel.QueueBindAsync(
                queue: b.QueueName,
                exchange: b.ExchangeName,
                routingKey: b.RoutingKey,
                arguments: b.Arguments,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
