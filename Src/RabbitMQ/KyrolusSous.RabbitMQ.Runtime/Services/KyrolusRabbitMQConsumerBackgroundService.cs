using System.Diagnostics;
using System.Text.Json;
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
/// Hosted background service managing the consumer lifecycle, scoped handler invocation, and resilient error recovery.
/// </summary>
/// <typeparam name="TConsumer">The consumer implementation type.</typeparam>
/// <typeparam name="TMessage">The message payload type.</typeparam>
public class KyrolusRabbitMQConsumerBackgroundService<TConsumer, TMessage> : BackgroundService
    where TConsumer : class, IKyrolusRabbitMQConsumer<TMessage>
    where TMessage : class
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

        ArgumentException.ThrowIfNullOrWhiteSpace(_consumerOptions.QueueName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting consumer service for {Consumer} on queue {Queue}",
            typeof(TConsumer).Name, _consumerOptions.QueueName);

        using var channel = await _connection.CreateChannelAsync(stoppingToken).ConfigureAwait(false);

        // Configure QoS
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _consumerOptions.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken).ConfigureAwait(false);

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

                var scope = _serviceProvider.CreateScope();
                try
                {
                    var consumerInstance = scope.ServiceProvider.GetRequiredService<TConsumer>();
                    await consumerInstance.HandleAsync(message, context, stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    if (scope is IAsyncDisposable asyncScope)
                    {
                        await asyncScope.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        scope.Dispose();
                    }
                }

                if (!_consumerOptions.AutoAck)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing message delivery {DeliveryTag} on queue {Queue}", ea.DeliveryTag, _consumerOptions.QueueName);
                KyrolusRabbitMQInstrumentation.SetActivityError(activity, ex);

                if (!_consumerOptions.AutoAck)
                {
                    try
                    {
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception nackEx)
                    {
                        _logger.LogError(nackEx, "Failed to nack message delivery {DeliveryTag}", ea.DeliveryTag);
                    }
                }
            }
        };

        var consumerTag = await channel.BasicConsumeAsync(_consumerOptions.QueueName, _consumerOptions.AutoAck, consumer, stoppingToken).ConfigureAwait(false);

        // Keep running until cancellation requested
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (stoppingToken.Register(s => ((TaskCompletionSource)s!).TrySetResult(), tcs))
        {
            await tcs.Task.ConfigureAwait(false);
        }

        try
        {
            await channel.BasicCancelAsync(consumerTag, noWait: false, CancellationToken.None).ConfigureAwait(false);
        }
        catch { }
    }
}
