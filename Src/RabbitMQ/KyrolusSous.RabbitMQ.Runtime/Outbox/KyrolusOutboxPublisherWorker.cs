using System.Security.Cryptography;
using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using KyrolusSous.RabbitMQ.Abstractions.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyrolusSous.RabbitMQ.Runtime.Outbox;

/// <summary>
/// Background worker that reliably processes and publishes pending transactional outbox messages with randomized jitter.
/// </summary>
public class KyrolusOutboxPublisherWorker : BackgroundService
{
    private readonly IKyrolusOutboxStore _outboxStore;
    private readonly IKyrolusRabbitMQUtils _rabbitMqUtils;
    private readonly ILogger<KyrolusOutboxPublisherWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private const int MaxErrorMessageLength = 4000;

    public KyrolusOutboxPublisherWorker(
        IKyrolusOutboxStore outboxStore,
        IKyrolusRabbitMQUtils rabbitMqUtils,
        ILogger<KyrolusOutboxPublisherWorker>? logger = null,
        TimeSpan? pollInterval = null)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _rabbitMqUtils = rabbitMqUtils ?? throw new ArgumentNullException(nameof(rabbitMqUtils));
        _logger = logger ?? NullLogger<KyrolusOutboxPublisherWorker>.Instance;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    private TimeSpan GetJitteredInterval()
    {
        // Add +/- 20% jitter
        var jitterFactor = 0.8 + (RandomNumberGenerator.GetInt32(0, 400) / 1000.0);
        return TimeSpan.FromMilliseconds(_pollInterval.TotalMilliseconds * jitterFactor);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kyrolus Outbox Publisher Worker started with polling interval {Interval}", _pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingMessages = await _outboxStore.GetPendingMessagesAsync(100, stoppingToken).ConfigureAwait(false);

                if (pendingMessages.Count > 0)
                {
                    foreach (var msg in pendingMessages)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        try
                        {
                            var headers = new Dictionary<string, object?>();
                            foreach (var (k, v) in msg.Headers)
                            {
                                headers[k] = v;
                            }

                            await _rabbitMqUtils.PublishAsync(
                                exchange: msg.Exchange,
                                routingKey: msg.RoutingKey,
                                body: msg.Payload,
                                correlationId: msg.Id,
                                headers: headers,
                                cancellationToken: stoppingToken).ConfigureAwait(false);

                            await _outboxStore.MarkAsProcessedAsync(msg.Id, stoppingToken).ConfigureAwait(false);
                            _logger.LogDebug("Outbox message {Id} published to {Exchange}/{RoutingKey}", msg.Id, msg.Exchange, msg.RoutingKey);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to publish outbox message {Id}", msg.Id);
                            var errorMsg = ex.ToString();
                            if (errorMsg.Length > MaxErrorMessageLength)
                            {
                                errorMsg = errorMsg[..MaxErrorMessageLength];
                            }

                            await _outboxStore.MarkAsFailedAsync(msg.Id, errorMsg, stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    await Task.Delay(GetJitteredInterval(), stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processing loop");
                await Task.Delay(GetJitteredInterval(), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
