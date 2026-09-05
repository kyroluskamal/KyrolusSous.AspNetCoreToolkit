namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// In-memory implementation of <see cref="IKyrolusOutboxStore"/> for testing and single-node applications.
/// </summary>
public sealed class KyrolusInMemoryOutboxStore : IKyrolusOutboxStore
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
        batchSize = Math.Clamp(batchSize, 1, KyrolusOutboxLimits.MaxBatchSize);
        var now = DateTimeOffset.UtcNow;

        var pending = _messages.Values
            .Where(m => m.Status == KyrolusOutboxMessageStatus.Pending || IsRetryDue(m, now))
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
                || IsRetryDue(msg, DateTimeOffset.UtcNow);

            if (!claimable) return Task.FromResult(false);

            msg.Status = KyrolusOutboxMessageStatus.Processing;
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Whether a <see cref="KyrolusOutboxMessageStatus.Failed"/> message's backoff window (if any) has
    /// elapsed as of <paramref name="now"/> - a message with no recorded <c>NextRetryAtUtc</c> (set
    /// before this feature existed, or never failed) is treated as immediately due, same as before
    /// backoff existed.
    /// </summary>
    private static bool IsRetryDue(KyrolusOutboxMessage message, DateTimeOffset now)
        => message.Status == KyrolusOutboxMessageStatus.Failed
        && (message.NextRetryAtUtc is null || message.NextRetryAtUtc <= now);

    public Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAtUtc, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.Status = KyrolusOutboxMessageStatus.Processed;
            msg.ProcessedOnUtc = processedAtUtc;
            msg.Error = null;
            msg.NextRetryAtUtc = null;
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            // Grouped under the same lock TryClaimAsync/RequeueAsync use on this object: Status,
            // RetryCount and NextRetryAtUtc must change together, or a concurrent GetPendingAsync/
            // TryClaimAsync could observe (e.g.) the incremented RetryCount with the still-stale
            // NextRetryAtUtc from the previous failure.
            lock (msg)
            {
                msg.Error = error;
                msg.RetryCount++;

                if (msg.RetryCount >= KyrolusOutboxLimits.MaxRetryCount)
                {
                    // Dead-lettered messages stop retrying altogether - there is no "next retry" to
                    // compute, and a stale NextRetryAtUtc left over from the prior failure must not
                    // linger and be misread as still meaningful.
                    msg.Status = KyrolusOutboxMessageStatus.DeadLettered;
                    msg.NextRetryAtUtc = null;
                }
                else
                {
                    msg.Status = KyrolusOutboxMessageStatus.Failed;
                    msg.NextRetryAtUtc = DateTimeOffset.UtcNow + ComputeBackoffDelay(msg.RetryCount);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Exponential backoff for the given (post-increment) retry count - see
    /// <see cref="KyrolusOutboxLimits.BackoffBaseSeconds"/>/<see cref="KyrolusOutboxLimits.MaxBackoffDelay"/>
    /// for the rationale behind the base and the cap.
    /// </summary>
    private static TimeSpan ComputeBackoffDelay(int retryCount)
    {
        var uncappedSeconds = Math.Pow(KyrolusOutboxLimits.BackoffBaseSeconds, retryCount);
        var cappedSeconds = Math.Min(uncappedSeconds, KyrolusOutboxLimits.MaxBackoffDelay.TotalSeconds);
        return TimeSpan.FromSeconds(cappedSeconds);
    }

    public Task<IReadOnlyList<KyrolusOutboxMessage>> GetDeadLetteredAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        batchSize = Math.Clamp(batchSize, 1, KyrolusOutboxLimits.MaxBatchSize);

        var deadLettered = _messages.Values
            .Where(m => m.Status == KyrolusOutboxMessageStatus.DeadLettered)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<KyrolusOutboxMessage>>(deadLettered);
    }

    public Task<bool> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (!_messages.TryGetValue(messageId, out var msg))
            return Task.FromResult(false);

        // Same discipline as TryClaimAsync: the "is it still DeadLettered" check and the reset to
        // Pending must happen as one indivisible step, or two concurrent requeue attempts could both
        // observe DeadLettered and both report success.
        lock (msg)
        {
            if (msg.Status != KyrolusOutboxMessageStatus.DeadLettered) return Task.FromResult(false);

            msg.Status = KyrolusOutboxMessageStatus.Pending;
            msg.RetryCount = 0;
            msg.Error = null;
            msg.NextRetryAtUtc = null;
            return Task.FromResult(true);
        }
    }

    public void Clear() => _messages.Clear();
}
