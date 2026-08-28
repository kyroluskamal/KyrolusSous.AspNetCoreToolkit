using System.Diagnostics;
using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using KyrolusSous.RabbitMQ.Abstractions.Models;
using KyrolusSous.RabbitMQ.Runtime.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Runtime.Services
{
    public class KyrolusRabbitMQUtils : IKyrolusRabbitMQUtils, global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQUtils
    {
        private readonly IKyrolusRabbitMQConnection _rabbitMqConnection;
        private readonly ILogger<KyrolusRabbitMQUtils> _logger;
        private readonly KyrolusRabbitMQOptions _options;
        private IChannel? _channel;
        private readonly SemaphoreSlim _channelLock = new(1, 1);

        public KyrolusRabbitMQUtils(
            IKyrolusRabbitMQConnection rabbitMqConnection,
            ILogger<KyrolusRabbitMQUtils>? logger = null,
            KyrolusRabbitMQOptions? options = null)
        {
            _rabbitMqConnection = rabbitMqConnection ?? throw new ArgumentNullException(nameof(rabbitMqConnection));
            _logger = logger ?? NullLogger<KyrolusRabbitMQUtils>.Instance;
            _options = options ?? new KyrolusRabbitMQOptions();
        }

        private async ValueTask<IChannel> GetChannelAsync(CancellationToken cancellationToken = default)
        {
            if (_channel is not null && _channel.IsOpen)
            {
                return _channel;
            }

            await _channelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_channel is not null && _channel.IsOpen)
                {
                    return _channel;
                }

                _channel = await _rabbitMqConnection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
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
            var channel = await GetChannelAsync().ConfigureAwait(false);

            using var activity = KyrolusRabbitMQInstrumentation.ActivitySource.StartActivity(
                $"RabbitMQ.Publish {exchange}/{routingKey}",
                ActivityKind.Producer);

            var headers = basicProperties?.Headers is not null
                ? new Dictionary<string, object?>(basicProperties.Headers)
                : new Dictionary<string, object?>();

            KyrolusRabbitMQInstrumentation.InjectTraceContext(headers, activity);

            var props = basicProperties ?? new BasicProperties();
            props.Headers = headers;
            props.DeliveryMode = DeliveryModes.Persistent;

            var json = JsonSerializer.Serialize(body);
            var bytes = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: mandatory,
                basicProperties: props,
                body: bytes).ConfigureAwait(false);

            _logger.LogDebug("Published message of type {Type} to exchange {Exchange} with routingKey {RoutingKey}",
                typeof(TEvent).Name, exchange, routingKey);
        }

        public async Task PublishAsync<TEvent>(
            string exchange,
            string routingKey,
            TEvent body,
            string? correlationId,
            IDictionary<string, object?>? headers = null,
            CancellationToken cancellationToken = default)
        {
            var channel = await GetChannelAsync(cancellationToken).ConfigureAwait(false);

            using var activity = KyrolusRabbitMQInstrumentation.ActivitySource.StartActivity(
                $"RabbitMQ.Publish {exchange}/{routingKey}",
                ActivityKind.Producer);

            var mergedHeaders = headers is not null
                ? new Dictionary<string, object?>(headers)
                : new Dictionary<string, object?>();

            KyrolusRabbitMQInstrumentation.InjectTraceContext(mergedHeaders, activity);

            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                Headers = mergedHeaders,
                DeliveryMode = DeliveryModes.Persistent
            };

            var json = JsonSerializer.Serialize(body);
            var bytes = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: props,
                body: bytes,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Published message of type {Type} to exchange {Exchange} with correlationId {CorrelationId}",
                typeof(TEvent).Name, exchange, correlationId);
        }

        public async Task PublishBatchAsync<TEvent>(
            string exchange,
            string routingKey,
            IEnumerable<TEvent> events,
            bool waitForConfirms = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(events);
            var channel = await GetChannelAsync(cancellationToken).ConfigureAwait(false);

            using var activity = KyrolusRabbitMQInstrumentation.ActivitySource.StartActivity(
                $"RabbitMQ.PublishBatch {exchange}/{routingKey}",
                ActivityKind.Producer);

            foreach (var @event in events)
            {
                var headers = new Dictionary<string, object?>();
                KyrolusRabbitMQInstrumentation.InjectTraceContext(headers, activity);

                var props = new BasicProperties
                {
                    Headers = headers,
                    DeliveryMode = DeliveryModes.Persistent
                };

                var json = JsonSerializer.Serialize(@event);
                var bytes = Encoding.UTF8.GetBytes(json);

                await channel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: props,
                    body: bytes,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task SetupQueueAsync(
            string exchange,
            IKyrolusQueueSetup[] queues,
            string type = ExchangeType.Direct,
            bool isDurable = true,
            bool autoDelete = false,
            IDictionary<string, object?>? arguments = null)
        {
            var channel = await GetChannelAsync().ConfigureAwait(false);

            // 1. Declare Main Exchange
            await channel.ExchangeDeclareAsync(
                exchange: exchange,
                type: type,
                durable: isDurable,
                autoDelete: autoDelete,
                arguments: arguments).ConfigureAwait(false);

            // 2. Declare Dead-Letter Exchange if enabled
            if (_options.UseDeadLetterExchange)
            {
                await channel.ExchangeDeclareAsync(
                    exchange: _options.DlxExchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false).ConfigureAwait(false);
            }

            // 3. Declare and bind queues
            foreach (var queue in queues)
            {
                var queueArgs = queue.Arguments is not null
                    ? new Dictionary<string, object?>(queue.Arguments)
                    : new Dictionary<string, object?>();

                if (_options.UseDeadLetterExchange)
                {
                    queueArgs["x-dead-letter-exchange"] = _options.DlxExchangeName;
                    queueArgs["x-dead-letter-routing-key"] = $"{_options.DlxRoutingKeyPrefix}{queue.RoutingKey}";

                    // Also declare and bind the corresponding Dead-Letter Queue
                    var dlqName = $"{queue.Name}.dlq";
                    await channel.QueueDeclareAsync(
                        queue: dlqName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false).ConfigureAwait(false);

                    await channel.QueueBindAsync(
                        queue: dlqName,
                        exchange: _options.DlxExchangeName,
                        routingKey: $"{_options.DlxRoutingKeyPrefix}{queue.RoutingKey}").ConfigureAwait(false);
                }

                await channel.QueueDeclareAsync(
                    queue: queue.Name,
                    durable: queue.Durable,
                    exclusive: queue.Exclusive,
                    autoDelete: queue.Autodelete,
                    arguments: queueArgs).ConfigureAwait(false);

                await channel.QueueBindAsync(
                    queue: queue.Name,
                    exchange: exchange,
                    routingKey: queue.RoutingKey).ConfigureAwait(false);
            }
        }
    }
}

namespace KyrolusSous.RabbitMQUtils.Services
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Runtime.Services.KyrolusRabbitMQUtils"/>.
    /// </summary>
    public class RabbitMQUtils : global::KyrolusSous.RabbitMQ.Runtime.Services.KyrolusRabbitMQUtils
    {
        public RabbitMQUtils(
            global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMQConnection rabbitMqConnection,
            Microsoft.Extensions.Logging.ILogger<global::KyrolusSous.RabbitMQ.Runtime.Services.KyrolusRabbitMQUtils>? logger = null,
            global::KyrolusSous.RabbitMQ.Abstractions.Models.KyrolusRabbitMQOptions? options = null)
            : base(rabbitMqConnection, logger, options)
        {
        }
    }
}
