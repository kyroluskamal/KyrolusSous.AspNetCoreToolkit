using System.Text.RegularExpressions;

namespace KyrolusSous.Repositories.EF.Runtime.TestApp;

public sealed class InMemoryCacheProvider : ICacheProvider
{
    private sealed record Entry(object? Value, DateTimeOffset? ExpiresAt, TimeSpan? SlidingExpiration, IReadOnlyCollection<string>? Tags);

    private readonly ConcurrentDictionary<string, Entry> store = new();

    public int Count => store.Count;

    public void Clear() => store.Clear();

    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (!TryGetEntry(cacheKey, out var entry)) return Task.FromResult(default(T?));
        if (entry.Value is null) return Task.FromResult(default(T?));
        return Task.FromResult((T?)entry.Value);
    }

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        var ttl = expirationTime > TimeSpan.Zero ? expirationTime : (TimeSpan?)null;
        SetEntry(cacheKey, value, ttl, null, null);
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        var ttl = ResolveTtl(value, options);
        var sliding = options?.SlidingExpiration;
        var tags = options?.Tags is null ? null : options.Tags.ToArray();
        SetEntry(cacheKey, value, ttl, sliding, tags);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        store.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        => Task.FromResult(TryGetEntry(cacheKey, out _));

    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyPattern)) return Task.CompletedTask;
        var regex = BuildRegex(keyPattern);
        foreach (var key in store.Keys.Where(key => regex.IsMatch(key)))
        {
            store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, T?>(StringComparer.Ordinal);
        foreach (var key in cacheKeys)
        {
            if (TryGetEntry(key, out var entry) && entry.Value is not null)
            {
                result[key] = (T?)entry.Value;
            }
            else
            {
                result[key] = default;
            }
        }

        return Task.FromResult<IDictionary<string, T?>>(result);
    }

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        var ttl = expirationTime > TimeSpan.Zero ? expirationTime : (TimeSpan?)null;
        foreach (var item in items)
        {
            SetEntry(item.Key, item.Value, ttl, null, null);
        }

        return Task.CompletedTask;
    }

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            var ttl = ResolveTtl(item.Value, options);
            var sliding = options?.SlidingExpiration;
            var tags = options?.Tags is null ? null : options.Tags.ToArray();
            SetEntry(item.Key, item.Value, ttl, sliding, tags);
        }

        return Task.CompletedTask;
    }

    public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        foreach (var key in cacheKeys)
        {
            store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag)) return Task.CompletedTask;
        foreach (var entry in store)
        {
            if (entry.Value.Tags is null) continue;
            if (entry.Value.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            {
                store.TryRemove(entry.Key, out _);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        KyrolusCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (TryGetEntry(cacheKey, out var entry))
        {
            if (entry.Value is null) return default!;
            return (T)entry.Value;
        }

        var value = await factory(cancellationToken).ConfigureAwait(false);
        if (value is null && options?.NegativeExpirationRelativeToNow is null)
        {
            return value;
        }

        await SetAsync(cacheKey, value, options, cancellationToken).ConfigureAwait(false);
        return value;
    }

    private bool TryGetEntry(string cacheKey, out Entry entry)
    {
        entry = default!;
        if (!store.TryGetValue(cacheKey, out var existing)) return false;

        if (IsExpired(existing))
        {
            store.TryRemove(cacheKey, out _);
            return false;
        }

        if (existing.SlidingExpiration is { } sliding && sliding > TimeSpan.Zero)
        {
            var refreshed = existing with { ExpiresAt = DateTimeOffset.UtcNow.Add(sliding) };
            store[cacheKey] = refreshed;
            entry = refreshed;
            return true;
        }

        entry = existing;
        return true;
    }

    private void SetEntry(string cacheKey, object? value, TimeSpan? ttl, TimeSpan? sliding, IReadOnlyCollection<string>? tags)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = ResolveExpiration(now, ttl, sliding);
        store[cacheKey] = new Entry(value, expiresAt, sliding, tags);
    }

    private static DateTimeOffset? ResolveExpiration(DateTimeOffset now, TimeSpan? ttl, TimeSpan? sliding)
    {
        if (sliding is { } slidingValue && slidingValue > TimeSpan.Zero)
        {
            return now.Add(slidingValue);
        }

        if (ttl is { } ttlValue && ttlValue > TimeSpan.Zero)
        {
            return now.Add(ttlValue);
        }

        return null;
    }

    private static bool IsExpired(Entry entry)
        => entry.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow;

    private static TimeSpan? ResolveTtl(object? value, KyrolusCacheEntryOptions? options)
    {
        if (options is null) return null;
        var ttl = value is null ? options.NegativeExpirationRelativeToNow : options.AbsoluteExpirationRelativeToNow;
        if (ttl is null) return null;

        if (options.Jitter is { } jitter && jitter > TimeSpan.Zero)
        {
            var extra = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * jitter.TotalMilliseconds);
            ttl = ttl.Value + extra;
        }

        return ttl;
    }

    private static Regex BuildRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern);
        var regexPattern = "^" + escaped.Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new Regex(regexPattern, RegexOptions.CultureInvariant);
    }
}

