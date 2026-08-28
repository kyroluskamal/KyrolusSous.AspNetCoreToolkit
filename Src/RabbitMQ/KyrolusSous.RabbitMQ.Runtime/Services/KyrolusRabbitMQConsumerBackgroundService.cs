using System.Diagnostics;
using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using KyrolusSous.RabbitMQ.Abstractions.Models;
using KyrolusSous.RabbitMQ.Runtime.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KyrolusSous.RabbitMQ.Runtime.Services;

/// <summary>
/// Background service that consumes strongly-typed messages from a RabbitMQ queue and dispatches them to scoped consumers.
/// </summary>
/// <typeparam name="TConsumer">The consumer type.</typeparam>
/// <typeparam name="TMessage">The message payload type.</typeparam>
public class KyrolusRabbitMQConsumerBackgroundService<TConsumer, TMessage> : BackgroundService
    where TConsumer : class, IKyrolusRabbitMQConsumer<TMessage>
{
    private readonly IKyrolusRabbitMQConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly KyrolusRabbitMQConsumerOptions _consumerOptions;
    private readonly ILogger<KyrolusRabbitMQConsumerBackgroundService<TConsumer, TMessage>> _logger;

    public KyrolusRabbitMQConsumerBackgroundService(
        IKyrolusRabbitMQConnection connection,
        IServiceProvider serviceProvider,
        KyrolusRabbitMQConsumerOptions consumerOptions,
        ILogger<KyrolusRabbitMQConsumerBackgroundService<TConsumer, TMessage>>? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _consumerOptions = consumerOptions ?? throw new ArgumentNullException(nameof(consumerOptions));
        _logger = logger ?? NullLogger<KyrolusRabbitMQConsumerBackgroundService<TConsumer, TMessage>>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_consumerOptions.QueueName))
        {
            _logger.LogWarning("Consumer queue name is not configured. Consumer service will not start.");
            return;
        }

        var channel = await _connection.CreateChannelAsync(stoppingToken).ConfigureAwait(false);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _consumerOptions.PrefetchCount, global: false, stoppingToken).ConfigureAwait(false);

        // Declare queue if needed
        await channel.QueueDeclareAsync(
            queue: _consumerOptions.QueueName,
            durable: _consumerOptions.Durable,
            exclusive: _consumerOptions.Exclusive,
            autoDelete: _consumerOptions.AutoDelete,
            arguments: _consumerOptions.Arguments,
            cancellationToken: stoppingToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_consumerOptions.ExchangeName) && !string.IsNullOrWhiteSpace(_consumerOptions.RoutingKey))
        {
            await channel.QueueBindAsync(
                queue: _consumerOptions.QueueName,
                exchange: _consumerOptions.ExchangeName,
                routingKey: _consumerOptions.RoutingKey,
                cancellationToken: stoppingToken).ConfigureAwait(false);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var headers = ea.BasicProperties.Headers is not null
                ? new Dictionary<string, object?>(ea.BasicProperties.Headers)
                : new Dictionary<string, object?>();

            var parentContext = KyrolusRabbitMQInstrumentation.ExtractTraceContext(headers);
            using var activity = KyrolusRabbitMQInstrumentation.ActivitySource.StartActivity(
                $"RabbitMQ.Consume {_consumerOptions.QueueName}",
                ActivityKind.Consumer,
                parentContext);

            var context = new KyrolusRabbitMQConsumeContext(
                Exchange: ea.Exchange,
                RoutingKey: ea.RoutingKey,
                DeliveryTag: ea.DeliveryTag,
                Redelivered: ea.Redelivered,
                MessageId: ea.BasicProperties.MessageId,
                CorrelationId: ea.BasicProperties.CorrelationId,
                TraceParent: activity?.Id,
                Headers: headers);

            try
            {
                var bodyText = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<TMessage>(bodyText);

                if (message is null)
                {
                    _logger.LogWarning("Deserialized null message in queue {Queue}", _consumerOptions.QueueName);
                    if (!_consumerOptions.AutoAck)
                    {
                        await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                    return;
                }

                using (var scope = _serviceProvider.CreateScope())
                {
                    var consumerInstance = scope.ServiceProvider.GetRequiredService<TConsumer>();
                    await consumerInstance.HandleAsync(message, context, stoppingToken).ConfigureAwait(false);
                }

                if (!_consumerOptions.AutoAck)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing message delivery {DeliveryTag} on queue {Queue}", ea.DeliveryTag, _consumerOptions.QueueName);
                if (!_consumerOptions.AutoAck)
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                }
            }
        };

        await channel.BasicConsumeAsync(_consumerOptions.QueueName, _consumerOptions.AutoAck, consumer, stoppingToken).ConfigureAwait(false);

        // Keep running until cancellation requested
        var tcs = new TaskCompletionSource();
        using (stoppingToken.Register(s => ((TaskCompletionSource)s!).TrySetResult(), tcs))
        {
            await tcs.Task.ConfigureAwait(false);
        }
    }
}
