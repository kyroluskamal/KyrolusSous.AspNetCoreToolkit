
namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusValidationMemoryCacheStore : IKyrolusValidationCacheStore
{
    private sealed record CacheEntry(IReadOnlyList<KyrolusValidationFailure> Failures, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>?> TryGetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(null);

        if (!entries.TryGetValue(key, out var entry))
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(null);

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            entries.TryRemove(key, out _);
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(null);
        }

        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(entry.Failures);
    }

    public ValueTask SetAsync(string key, IReadOnlyList<KyrolusValidationFailure> failures, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || ttl <= TimeSpan.Zero) return ValueTask.CompletedTask;

        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        entries[key] = new CacheEntry(failures, expiresAt);
        return ValueTask.CompletedTask;
    }
}
