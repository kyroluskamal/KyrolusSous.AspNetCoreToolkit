using System.Text;
using System.Text.Json;
using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;
using KyrolusSous.IRabbitMQUtilsInterfaces.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KyrolusSous.RabbitMQUtils.Services;

public class RabbitMqListener : IKyrolusRabbitMqListener, IRabbitMqListener
{
    private readonly IKyrolusRabbitMQConnection _rabbitMqConnection;
    private readonly ILogger<RabbitMqListener> _logger;
    private readonly KyrolusRabbitMQOptions _options;

    public RabbitMqListener(
        IKyrolusRabbitMQConnection rabbitMqConnection,
        ILogger<RabbitMqListener>? logger = null,
        KyrolusRabbitMQOptions? options = null)
    {
        _rabbitMqConnection = rabbitMqConnection ?? throw new ArgumentNullException(nameof(rabbitMqConnection));
        _logger = logger ?? NullLogger<RabbitMqListener>.Instance;
        _options = options ?? new KyrolusRabbitMQOptions();
    }

    public async Task ConsumeAsync<TEvent>(
        string queue,
        Func<TEvent, Task> action,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        bool autoAck = false,
        IDictionary<string, object?>? arguments = null)
    {
        await ConsumeWithContextAsync<TEvent>(
            queue,
            (evt, _) => action(evt),
            durable,
            exclusive,
            autoDelete,
            autoAck,
            arguments);
    }

    public async Task ConsumeWithContextAsync<TEvent>(
        string queue,
        Func<TEvent, IDictionary<string, object?>?, Task> action,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        bool autoAck = false,
        IDictionary<string, object?>? arguments = null)
    {
        var channel = await _rabbitMqConnection.CreateChannelAsync();
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);
        await channel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                TEvent? eventToPublish = default;
                try
                {
                    // First try to deserialize as envelope if present
                    if (message.Contains("\"Payload\"") && message.Contains("\"MessageId\""))
                    {
                        var envelope = JsonSerializer.Deserialize<KyrolusMessageEnvelope<TEvent>>(message);
                        if (envelope is not null && envelope.Payload is not null)
                        {
                            eventToPublish = envelope.Payload;
                        }
                    }
                }
                catch
                {
                    // Fall back to direct deserialization
                }

                if (Equals(eventToPublish, default(TEvent)))
                {
                    eventToPublish = JsonSerializer.Deserialize<TEvent>(message);
                }

                if (!Equals(eventToPublish, default(TEvent)))
                {
                    _logger.LogDebug("Processing message from queue {Queue}", queue);
                    await action(eventToPublish!, ea.BasicProperties.Headers);

                    if (!autoAck)
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    }
                }
                else
                {
                    _logger.LogWarning("Received null or unparseable message from queue {Queue}, rejecting to DLQ", queue);
                    // Requeue = false pushes to Dead Letter Exchange
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {Queue}: {Message}", queue, ex.Message);
                // Requeue = false so it routes to DLX instead of poison infinite retry
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(queue: queue, autoAck: autoAck, consumer: consumer);
        _logger.LogInformation("Listening on queue {Queue} (autoAck={AutoAck})", queue, autoAck);
    }
}
