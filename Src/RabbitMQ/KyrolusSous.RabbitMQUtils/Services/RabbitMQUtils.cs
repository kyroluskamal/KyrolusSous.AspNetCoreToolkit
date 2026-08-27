using System.Text;
using System.Text.Json;
using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;
using KyrolusSous.IRabbitMQUtilsInterfaces.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQUtils.Services;

public class RabbitMQUtils : IKyrolusRabbitMQUtils, IRabbitMQUtils
{
    private readonly IKyrolusRabbitMQConnection _rabbitMqConnection;
    private readonly ILogger<RabbitMQUtils> _logger;
    private readonly KyrolusRabbitMQOptions _options;
    private IChannel? _channel;
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    public RabbitMQUtils(
        IKyrolusRabbitMQConnection rabbitMqConnection,
        ILogger<RabbitMQUtils>? logger = null,
        KyrolusRabbitMQOptions? options = null)
    {
        _rabbitMqConnection = rabbitMqConnection ?? throw new ArgumentNullException(nameof(rabbitMqConnection));
        _logger = logger ?? NullLogger<RabbitMQUtils>.Instance;
        _options = options ?? new KyrolusRabbitMQOptions();
    }

    private async ValueTask<IChannel> GetChannelAsync(CancellationToken cancellationToken = default)
    {
        if (_channel is not null && !_channel.IsClosed)
        {
            return _channel;
        }

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is not null && !_channel.IsClosed)
            {
                return _channel;
            }

            _channel = await _rabbitMqConnection.CreateChannelAsync(cancellationToken);
            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task PublishAsync<TEvent>(
        string exchange,
        string routingKey,
        TEvent body,
        bool mandatory = true,
        BasicProperties? basicProperties = null)
    {
        var channel = await GetChannelAsync();
        var message = JsonSerializer.Serialize(body);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        var props = basicProperties ?? new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json"
        };

        _logger.LogDebug("Publishing message to exchange {Exchange} with routing key {RoutingKey}", exchange, routingKey);

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: mandatory,
            basicProperties: props,
            body: messageBytes);
    }

    public async Task PublishAsync<TEvent>(
        string exchange,
        string routingKey,
        TEvent body,
        string? correlationId,
        IDictionary<string, object?>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var channel = await GetChannelAsync(cancellationToken);
        var envelope = new KyrolusMessageEnvelope<TEvent>(body, correlationId);
        if (headers is not null)
        {
            foreach (var kvp in headers)
            {
                if (kvp.Value is not null)
                {
                    envelope.Headers[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
                }
            }
        }

        var message = JsonSerializer.Serialize(envelope);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            CorrelationId = correlationId ?? envelope.MessageId,
            MessageId = envelope.MessageId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: messageBytes,
            cancellationToken: cancellationToken);
    }

    public async Task SetupQueueAsync(
        string exchange,
        IQueueSetup[] queues,
        string type = ExchangeType.Direct,
        bool isDurable = true,
        bool autoDelete = false,
        IDictionary<string, object?>? arguments = null)
    {
        var channel = await GetChannelAsync();

        // 1. Declare primary exchange
        await channel.ExchangeDeclareAsync(exchange, type, isDurable, autoDelete, arguments);

        // 2. Setup Dead Letter Exchange if configured
        if (_options.UseDeadLetterExchange)
        {
            await channel.ExchangeDeclareAsync(_options.DlxExchangeName, ExchangeType.Direct, durable: true, autoDelete: false);
        }

        // 3. Declare and bind queues
        foreach (var q in queues)
        {
            var queueArgs = q.Arguments is not null
                ? new Dictionary<string, object?>(q.Arguments)
                : new Dictionary<string, object?>();

            if (_options.UseDeadLetterExchange && !queueArgs.ContainsKey("x-dead-letter-exchange"))
            {
                queueArgs["x-dead-letter-exchange"] = _options.DlxExchangeName;
                queueArgs["x-dead-letter-routing-key"] = $"{_options.DlxRoutingKeyPrefix}{q.Name}";

                // Also declare dead letter queue and bind to DLX
                var dlqName = $"{q.Name}.dlq";
                await channel.QueueDeclareAsync(dlqName, durable: true, exclusive: false, autoDelete: false);
                await channel.QueueBindAsync(dlqName, _options.DlxExchangeName, $"{_options.DlxRoutingKeyPrefix}{q.Name}");
            }

            await channel.QueueDeclareAsync(q.Name, q.Durable, q.Exclusive, q.Autodelete, queueArgs);
            await channel.QueueBindAsync(q.Name, exchange, q.RoutingKey);
        }
    }
}
