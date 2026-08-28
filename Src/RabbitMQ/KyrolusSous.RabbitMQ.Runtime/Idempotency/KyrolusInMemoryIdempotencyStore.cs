using System.Collections.Concurrent;
using KyrolusSous.RabbitMQ.Abstractions.Idempotency;

namespace KyrolusSous.RabbitMQ.Runtime.Idempotency;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IKyrolusIdempotencyStore"/> with expiration.
/// </summary>
public class KyrolusInMemoryIdempotencyStore : IKyrolusIdempotencyStore
{
    private sealed record LockEntry(DateTimeOffset Expiry);
    private sealed record ResultEntry(string Result, DateTimeOffset? Expiry);

    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();
    private readonly ConcurrentDictionary<string, ResultEntry> _results = new();

    public Task<bool> TryAcquireLockAsync(string idempotencyKey, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var now = DateTimeOffset.UtcNow;
        var newExpiry = now.Add(lockDuration);

        while (true)
        {
            if (_locks.TryGetValue(idempotencyKey, out var existing))
            {
                if (existing.Expiry > now)
                {
                    return Task.FromResult(false); // Lock is active
                }

                // Expired lock: try to update
                if (_locks.TryUpdate(idempotencyKey, new LockEntry(newExpiry), existing))
                {
                    return Task.FromResult(true);
                }
            }
            else
            {
                // Try to insert
                if (_locks.TryAdd(idempotencyKey, new LockEntry(newExpiry)))
                {
                    return Task.FromResult(true);
                }
            }
        }
    }

    public Task SetResultAsync(string idempotencyKey, string result, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var expiryTime = expiry.HasValue ? DateTimeOffset.UtcNow.Add(expiry.Value) : (DateTimeOffset?)null;
        _results[idempotencyKey] = new ResultEntry(result, expiryTime);

        // Release lock upon setting result
        _locks.TryRemove(idempotencyKey, out _);

        return Task.CompletedTask;
    }

    public Task<string?> GetResultAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (_results.TryGetValue(idempotencyKey, out var entry))
        {
            if (entry.Expiry.HasValue && entry.Expiry.Value < DateTimeOffset.UtcNow)
            {
                _results.TryRemove(idempotencyKey, out _);
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(entry.Result);
        }

        return Task.FromResult<string?>(null);
    }

    public Task ReleaseLockAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        _locks.TryRemove(idempotencyKey, out _);
        return Task.CompletedTask;
    }
}
