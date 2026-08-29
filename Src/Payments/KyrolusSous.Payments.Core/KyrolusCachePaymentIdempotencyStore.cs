using System.Collections.Concurrent;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusCachePaymentIdempotencyStore(IKyrolusCacheProvider? cacheProvider = null) : IKyrolusPaymentIdempotencyStore
{
    private readonly ConcurrentDictionary<string, (KyrolusPaymentResult Result, DateTimeOffset Expiry)> _memoryStore = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _activeLocks = new();

    public async Task<KyrolusPaymentResult?> GetResultAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;

        var key = $"kyrolus:payment:idempotency:{idempotencyKey}";
        if (cacheProvider is not null)
        {
            var cached = await cacheProvider.GetAsync<KyrolusPaymentResult>(key, cancellationToken).ConfigureAwait(false);
            if (cached is not null) return cached;
        }

        if (_memoryStore.TryGetValue(idempotencyKey, out var entry))
        {
            if (DateTimeOffset.UtcNow <= entry.Expiry)
            {
                return entry.Result;
            }
            _memoryStore.TryRemove(idempotencyKey, out _);
        }

        return null;
    }

    public async Task SaveResultAsync(string idempotencyKey, KyrolusPaymentResult result, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return;

        var ttl = expiration ?? TimeSpan.FromHours(24);
        var key = $"kyrolus:payment:idempotency:{idempotencyKey}";

        if (cacheProvider is not null)
        {
            await cacheProvider.SetAsync(key, result, ttl, cancellationToken).ConfigureAwait(false);
        }

        _memoryStore[idempotencyKey] = (result, DateTimeOffset.UtcNow.Add(ttl));
        _activeLocks.TryRemove(idempotencyKey, out _);
    }

    public Task<bool> TryAcquireLockAsync(string idempotencyKey, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return Task.FromResult(true);

        var now = DateTimeOffset.UtcNow;
        var expiry = now.Add(lockDuration);

        while (true)
        {
            if (_activeLocks.TryGetValue(idempotencyKey, out var currentExpiry))
            {
                if (now < currentExpiry)
                {
                    return Task.FromResult(false); // Locked
                }

                if (_activeLocks.TryUpdate(idempotencyKey, expiry, currentExpiry))
                {
                    return Task.FromResult(true);
                }
            }
            else
            {
                if (_activeLocks.TryAdd(idempotencyKey, expiry))
                {
                    return Task.FromResult(true);
                }
            }
        }
    }

    public Task ReleaseLockAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _activeLocks.TryRemove(idempotencyKey, out _);
        }
        return Task.CompletedTask;
    }
}
