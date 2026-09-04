namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// In-memory implementation of <see cref="IOutboxStore"/> for testing and single-node applications.
/// </summary>
public sealed class KyrolusInMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentDictionary<Guid, KyrolusOutboxMessage> _messages = new();

    public IReadOnlyCollection<KyrolusOutboxMessage> AllMessages => [.. _messages.Values];

    public Task SaveAsync(KyrolusOutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KyrolusOutboxMessage>> GetPendingAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var pending = _messages.Values
            .Where(m => m.Status == KyrolusOutboxMessageStatus.Pending || (m.Status == KyrolusOutboxMessageStatus.Failed && m.RetryCount < 5))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<KyrolusOutboxMessage>>(pending);
    }

    public Task<bool> TryClaimAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (!_messages.TryGetValue(messageId, out var msg))
            return Task.FromResult(false);

        // The read (Status/RetryCount) and the write (Status = Processing) must happen as one
        // indivisible step - otherwise two concurrent callers can both observe "claimable" before
        // either writes Processing, and both proceed to process the same message.
        lock (msg)
        {
            var claimable = msg.Status == KyrolusOutboxMessageStatus.Pending
                || (msg.Status == KyrolusOutboxMessageStatus.Failed && msg.RetryCount < 5);

            if (!claimable) return Task.FromResult(false);

            msg.Status = KyrolusOutboxMessageStatus.Processing;
            return Task.FromResult(true);
        }
    }

    public Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAtUtc, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.Status = KyrolusOutboxMessageStatus.Processed;
            msg.ProcessedOnUtc = processedAtUtc;
            msg.Error = null;
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.Status = KyrolusOutboxMessageStatus.Failed;
            msg.Error = error;
            msg.RetryCount++;
        }

        return Task.CompletedTask;
    }

    public void Clear() => _messages.Clear();
}
