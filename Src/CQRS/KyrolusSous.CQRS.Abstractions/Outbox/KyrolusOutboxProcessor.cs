using System.Text.Json;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Worker service that processes pending outbox messages and dispatches them via mediator.
/// </summary>
public sealed class KyrolusOutboxProcessor(
    IOutboxStore outboxStore,
    IKyrolusMediatorPublisher publisher,
    ILogger<KyrolusOutboxProcessor>? logger = null)
{
    private readonly IOutboxStore _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
    private readonly IKyrolusMediatorPublisher _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    private readonly ILogger? _logger = logger;

    /// <summary>
    /// Executes a single processing pass over pending outbox messages.
    /// </summary>
    public async Task<int> ProcessPendingMessagesAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var pending = await _outboxStore.GetPendingAsync(batchSize, cancellationToken).ConfigureAwait(false);
        if (pending.Count == 0) return 0;

        var processedCount = 0;

        foreach (var message in pending)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var eventType = Type.GetType(message.EventType)
                    ?? AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType(message.EventType))
                        .FirstOrDefault(t => t is not null);

                if (eventType is null)
                {
                    await _outboxStore.MarkFailedAsync(
                        message.Id,
                        $"Could not resolve event type '{message.EventType}'.",
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var eventInstance = JsonSerializer.Deserialize(message.Payload, eventType);
                if (eventInstance is null)
                {
                    await _outboxStore.MarkFailedAsync(
                        message.Id,
                        $"Deserialized event payload was null for type '{message.EventType}'.",
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await _publisher.PublishAsync(eventInstance, cancellationToken).ConfigureAwait(false);
                await _outboxStore.MarkProcessedAsync(message.Id, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[Kyrolus CQRS Outbox] Failed to process outbox message {MessageId} ({EventType})", message.Id, message.EventType);
                await _outboxStore.MarkFailedAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
            }
        }

        return processedCount;
    }
}
