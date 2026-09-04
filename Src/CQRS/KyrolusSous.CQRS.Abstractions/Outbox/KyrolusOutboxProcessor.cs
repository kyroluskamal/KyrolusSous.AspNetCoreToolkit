namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Worker service that processes pending outbox messages and dispatches them via mediator.
/// </summary>
/// <remarks>
/// Resolves each message's stored <c>EventType</c> name through <paramref name="typeRegistry"/> - an
/// explicit allow-list - rather than searching every loaded assembly for a type of that name and
/// deserializing straight into whatever it finds. A store an application does not fully trust (rows
/// come from another service, a queue, or anywhere not written exclusively by this process's own
/// domain-event dispatch) should never hand an arbitrary type name to a deserializer; a message naming
/// a type outside the registry is marked failed instead of resolved.
/// <para>
/// <paramref name="typeRegistry"/> is optional only for backward compatibility with code built before
/// it existed. Passing <see langword="null"/> falls back to scanning every currently loaded assembly
/// for <see cref="IKyrolusNotification"/> types the first time it is needed - narrower than the old
/// "any type at all" behavior, but still an implicit, unconfigured allow-list. Pass an explicit
/// registry (built from just the assemblies that define real outbox events) for tighter control.
/// </para>
/// </remarks>
public sealed class KyrolusOutboxProcessor(
    IOutboxStore outboxStore,
    IKyrolusMediatorPublisher publisher,
    IKyrolusOutboxEventTypeRegistry? typeRegistry = null,
    ILogger<KyrolusOutboxProcessor>? logger = null)
{
    private readonly IOutboxStore _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
    private readonly IKyrolusMediatorPublisher _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    private readonly ILogger? _logger = logger;
    private readonly Lazy<IKyrolusOutboxEventTypeRegistry> _typeRegistry = new(
        () => typeRegistry ?? KyrolusOutboxEventTypeRegistry.FromAssemblies(AppDomain.CurrentDomain.GetAssemblies()));

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

            using var activity = KyrolusCqrsTelemetry.ActivitySource.StartActivity("Outbox message");
            activity?.SetTag(KyrolusCqrsTelemetry.TagOutboxEventType, message.EventType);

            try
            {
                // Claim before touching the message: without this, an overlapping pass (a slow prior
                // run still in flight, or another instance against a shared store) could read and
                // publish the same message a second time.
                if (!await _outboxStore.TryClaimAsync(message.Id, cancellationToken).ConfigureAwait(false))
                {
                    RecordOutcome(activity, message.EventType, "skipped-already-claimed");
                    continue;
                }

                if (!_typeRegistry.Value.TryResolve(message.EventType, out var eventType) || eventType is null)
                {
                    await _outboxStore.MarkFailedAsync(
                        message.Id,
                        $"Event type '{message.EventType}' is not in the outbox type registry's allow-list.",
                        cancellationToken).ConfigureAwait(false);
                    RecordOutcome(activity, message.EventType, "failed");
                    continue;
                }

                var eventInstance = JsonSerializer.Deserialize(message.Payload, eventType);
                if (eventInstance is null)
                {
                    await _outboxStore.MarkFailedAsync(
                        message.Id,
                        $"Deserialized event payload was null for type '{message.EventType}'.",
                        cancellationToken).ConfigureAwait(false);
                    RecordOutcome(activity, message.EventType, "failed");
                    continue;
                }

                await _publisher.PublishAsync(eventInstance, cancellationToken).ConfigureAwait(false);
                await _outboxStore.MarkProcessedAsync(message.Id, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
                processedCount++;
                RecordOutcome(activity, message.EventType, "processed");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[Kyrolus CQRS Outbox] Failed to process outbox message {MessageId} ({EventType})", message.Id, message.EventType);
                await _outboxStore.MarkFailedAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                RecordOutcome(activity, message.EventType, "failed");
            }
        }

        return processedCount;
    }

    private static void RecordOutcome(Activity? activity, string eventType, string outcome)
    {
        activity?.SetTag(KyrolusCqrsTelemetry.TagOutboxOutcome, outcome);
        KyrolusCqrsTelemetry.OutboxMessagesProcessed.Add(
            1,
            new KeyValuePair<string, object?>(KyrolusCqrsTelemetry.TagOutboxEventType, eventType),
            new KeyValuePair<string, object?>(KyrolusCqrsTelemetry.TagOutboxOutcome, outcome));
    }
}
