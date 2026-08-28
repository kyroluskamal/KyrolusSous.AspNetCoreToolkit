using System.Diagnostics;
using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using KyrolusSous.RabbitMQ.Abstractions.Models;
using KyrolusSous.RabbitMQ.Runtime.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KyrolusSous.RabbitMQ.Runtime.Services
{
    public class KyrolusRabbitMqListener : IKyrolusRabbitMqListener, global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMqListener
    {
        private readonly IKyrolusRabbitMQConnection _rabbitMqConnection;
        private readonly ILogger<KyrolusRabbitMqListener> _logger;
        private readonly KyrolusRabbitMQOptions _options;

        public KyrolusRabbitMqListener(
            IKyrolusRabbitMQConnection rabbitMqConnection,
            ILogger<KyrolusRabbitMqListener>? logger = null,
            KyrolusRabbitMQOptions? options = null)
        {
            _rabbitMqConnection = rabbitMqConnection ?? throw new ArgumentNullException(nameof(rabbitMqConnection));
            _logger = logger ?? NullLogger<KyrolusRabbitMqListener>.Instance;
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
                arguments).ConfigureAwait(false);
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
            var channel = await _rabbitMqConnection.CreateChannelAsync().ConfigureAwait(false);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false).ConfigureAwait(false);
            await channel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var parentContext = KyrolusRabbitMQInstrumentation.ExtractTraceContext(ea.BasicProperties.Headers);
                using var activity = KyrolusRabbitMQInstrumentation.ActivitySource.StartActivity(
                    $"RabbitMQ.Consume {queue}",
                    ActivityKind.Consumer,
                    parentContext);

                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<TEvent>(body);

                    if (@event is null)
                    {
                        _logger.LogWarning("Deserialized null event from queue {Queue}", queue);
                        if (!autoAck)
                        {
                            await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false).ConfigureAwait(false);
                        }
                        return;
                    }

                    await action(@event, ea.BasicProperties.Headers).ConfigureAwait(false);

                    if (!autoAck)
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event from queue {Queue}", queue);
                    if (!autoAck)
                    {
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false).ConfigureAwait(false);
                    }
                }
            };

            await channel.BasicConsumeAsync(queue, autoAck, consumer).ConfigureAwait(false);
        }
    }
}

namespace KyrolusSous.RabbitMQUtils.Services
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Runtime.Services.KyrolusRabbitMqListener"/>.
    /// </summary>
    public class RabbitMqListener : global::KyrolusSous.RabbitMQ.Runtime.Services.KyrolusRabbitMqListener
    {
        public RabbitMqListener(
            global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMQConnection rabbitMqConnection,
            Microsoft.Extensions.Logging.ILogger<global::KyrolusSous.RabbitMQ.Runtime.Services.KyrolusRabbitMqListener>? logger = null,
            global::KyrolusSous.RabbitMQ.Abstractions.Models.KyrolusRabbitMQOptions? options = null)
            : base(rabbitMqConnection, logger, options)
        {
        }
    }
}
