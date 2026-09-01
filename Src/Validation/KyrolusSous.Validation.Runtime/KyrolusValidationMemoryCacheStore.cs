
namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// Default <see cref="IKyrolusValidationCacheStore"/> implementation: an in-process <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// with per-entry expiry. Registered automatically by <see cref="ServiceCollectionExtensions.AddKyrolusValidationRuntime"/>.
/// Lives only in this process's memory - in a multi-instance deployment, each instance has its own independent
/// cache. For a cache shared across instances (e.g. Redis), use <c>KyrolusSous.Validation.Caching</c>'s
/// <c>AddKyrolusValidationDistributedCache()</c> to replace this registration.
/// </summary>
public sealed class KyrolusValidationMemoryCacheStore : IKyrolusValidationCacheStore
{
    /// <summary>A cached outcome: the failures it represents, and the UTC instant after which it's stale.</summary>
    private sealed record CacheEntry(IReadOnlyList<KyrolusValidationFailure> Failures, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>?> TryGetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || !entries.TryGetValue(key, out var entry))
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(null);

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            entries.TryRemove(key, out _);
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(null);
        }

        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(entry.Failures);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(string key, IReadOnlyList<KyrolusValidationFailure> failures, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || ttl <= TimeSpan.Zero) return ValueTask.CompletedTask;

        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        entries[key] = new CacheEntry(failures, expiresAt);
        return ValueTask.CompletedTask;
    }
}
