
namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusValidationMemoryCacheStore : IKyrolusValidationCacheStore
{
    private sealed record CacheEntry(IReadOnlyList<KyrolusValidationFailure> Failures, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);

    public bool TryGet(string key, out IReadOnlyList<KyrolusValidationFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            failures = Array.Empty<KyrolusValidationFailure>();
            return false;
        }

        if (!entries.TryGetValue(key, out var entry))
        {
            failures = Array.Empty<KyrolusValidationFailure>();
            return false;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            entries.TryRemove(key, out _);
            failures = Array.Empty<KyrolusValidationFailure>();
            return false;
        }

        failures = entry.Failures;
        return true;
    }

    public void Set(string key, IReadOnlyList<KyrolusValidationFailure> failures, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(key) || ttl <= TimeSpan.Zero) return;

        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        entries[key] = new CacheEntry(failures, expiresAt);
    }
}
