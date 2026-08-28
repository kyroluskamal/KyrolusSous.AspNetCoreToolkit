using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

internal sealed class InMemoryIntegrationCacheProvider : IKyrolusCacheProvider
{
    private readonly ConcurrentDictionary<string, object?> store = new(StringComparer.Ordinal);

    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (!store.TryGetValue(cacheKey, out var value) || value is null)
        {
            return Task.FromResult(default(T?));
        }

        return Task.FromResult((T?)value);
    }

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        store[cacheKey] = value;
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        store[cacheKey] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        store.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        => Task.FromResult(store.ContainsKey(cacheKey));

    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyPattern))
        {
            return Task.CompletedTask;
        }

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
            result[key] = store.TryGetValue(key, out var value) && value is not null ? (T?)value : default;
        }

        return Task.FromResult<IDictionary<string, T?>>(result);
    }

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            store[item.Key] = item.Value;
        }

        return Task.CompletedTask;
    }

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            store[item.Key] = item.Value;
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
        => Task.CompletedTask;

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        KyrolusCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (store.TryGetValue(cacheKey, out var existing))
        {
            if (existing is null)
            {
                return default!;
            }

            return (T)existing;
        }

        var value = await factory(cancellationToken).ConfigureAwait(false);
        store[cacheKey] = value;
        return value;
    }

    private static Regex BuildRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern);
        var regexPattern = "^" + escaped.Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new Regex(regexPattern, RegexOptions.CultureInvariant);
    }
}
