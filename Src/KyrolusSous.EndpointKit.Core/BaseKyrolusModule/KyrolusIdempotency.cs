using System.Collections.Concurrent;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public sealed record KyrolusIdempotencyEntry(object? Value, int StatusCode, string? ContentType);

public interface IKyrolusIdempotencyStore
{
    Task<KyrolusIdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, KyrolusIdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public sealed class KyrolusInMemoryIdempotencyStore : IKyrolusIdempotencyStore
{
    private readonly ConcurrentDictionary<string, (KyrolusIdempotencyEntry Entry, DateTimeOffset ExpiresAt)> store = new();

    public Task<KyrolusIdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!store.TryGetValue(key, out var entry))
        {
            return Task.FromResult<KyrolusIdempotencyEntry?>(null);
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            store.TryRemove(key, out _);
            return Task.FromResult<KyrolusIdempotencyEntry?>(null);
        }

        return Task.FromResult<KyrolusIdempotencyEntry?>(entry.Entry);
    }

    public Task SetAsync(string key, KyrolusIdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        store[key] = (entry, expiresAt);
        return Task.CompletedTask;
    }
}
