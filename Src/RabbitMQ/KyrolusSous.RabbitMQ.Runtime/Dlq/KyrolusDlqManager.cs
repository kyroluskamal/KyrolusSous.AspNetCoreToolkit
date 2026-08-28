using KyrolusSous.RabbitMQ.Abstractions.Dlq;
using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Runtime.Dlq;

/// <summary>
/// Managed implementation of <see cref="IKyrolusDlqManager"/> for DLQ inspection and replay operations.
/// </summary>
public class KyrolusDlqManager : IKyrolusDlqManager
{
    private readonly IKyrolusRabbitMQConnection _connection;
    private readonly ILogger<KyrolusDlqManager> _logger;

    public KyrolusDlqManager(
        IKyrolusRabbitMQConnection connection,
        ILogger<KyrolusDlqManager>? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? NullLogger<KyrolusDlqManager>.Instance;
    }

    public async Task<uint> GetDeadLetterMessageCountAsync(string dlqName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dlqName);
        using var channel = await _connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);

        var result = await channel.QueueDeclarePassiveAsync(dlqName, cancellationToken).ConfigureAwait(false);
        return result.MessageCount;
    }

    public async Task<int> ReplayDeadLetterMessagesAsync(
        string dlqName,
        string targetExchange,
        string targetRoutingKey,
        int maxMessages = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dlqName);
        ArgumentNullException.ThrowIfNull(targetExchange);
        ArgumentNullException.ThrowIfNull(targetRoutingKey);

        using var channel = await _connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
        int replayedCount = 0;

        for (int i = 0; i < maxMessages; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var getResult = await channel.BasicGetAsync(dlqName, autoAck: false, cancellationToken).ConfigureAwait(false);
            if (getResult == null)
            {
                break; // DLQ is empty
            }

            try
            {
                var props = new BasicProperties
                {
                    CorrelationId = getResult.BasicProperties.CorrelationId,
                    MessageId = getResult.BasicProperties.MessageId,
                    Type = getResult.BasicProperties.Type,
                    ContentType = getResult.BasicProperties.ContentType,
                    ContentEncoding = getResult.BasicProperties.ContentEncoding,
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers = getResult.BasicProperties.Headers != null ? new Dictionary<string, object?>(getResult.BasicProperties.Headers) : null
                };

                // Re-publish to target exchange & routing key
                await channel.BasicPublishAsync(
                    exchange: targetExchange,
                    routingKey: targetRoutingKey,
                    mandatory: true,
                    basicProperties: props,
                    body: getResult.Body,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Acknowledge removal from DLQ
                await channel.BasicAckAsync(getResult.DeliveryTag, multiple: false, cancellationToken).ConfigureAwait(false);
                replayedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed replaying message {DeliveryTag} from DLQ {DlqName}", getResult.DeliveryTag, dlqName);
                await channel.BasicNackAsync(getResult.DeliveryTag, multiple: false, requeue: true, cancellationToken).ConfigureAwait(false);
                break;
            }
        }

        _logger.LogInformation("Successfully replayed {Count} messages from {DlqName} to {TargetExchange}/{TargetRoutingKey}",
            replayedCount, dlqName, targetExchange, targetRoutingKey);

        return replayedCount;
    }

    public async Task PurgeDeadLetterQueueAsync(string dlqName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dlqName);
        using var channel = await _connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);

        await channel.QueuePurgeAsync(dlqName, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Purged DLQ {DlqName}", dlqName);
    }
}
