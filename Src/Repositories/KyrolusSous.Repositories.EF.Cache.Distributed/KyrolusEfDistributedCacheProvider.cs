using System.Text.Json;
using KyrolusSous.Caching.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace KyrolusSous.Repositories.EF.Cache.Distributed;

public sealed class KyrolusEfDistributedCacheProvider : IKyrolusCacheProvider
{
    private static readonly byte[] NullPayload = [0];
    private readonly IDistributedCache cache;
    private readonly JsonSerializerOptions json;

    public KyrolusEfDistributedCacheProvider(IDistributedCache cache, JsonSerializerOptions? options = null)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        json = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public async Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
    {
        var payload = await GetPayloadAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (payload is null) return default;
        if (IsNullPayload(payload)) return default;
        return JsonSerializer.Deserialize<T>(payload, json);
    }

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        var entryOptions = new DistributedCacheEntryOptions();
        if (expirationTime > TimeSpan.Zero)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = expirationTime;
        }

        return SetPayloadAsync(cacheKey, value, entryOptions, cancellationToken);
    }

    public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        var entryOptions = BuildEntryOptions(value, options);
        if (entryOptions is null)
        {
            return Task.CompletedTask;
        }

        return SetPayloadAsync(cacheKey, value, entryOptions, cancellationToken);
    }

    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(ValidateKey(cacheKey), cancellationToken);

    public async Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        => await GetPayloadAsync(cacheKey, cancellationToken).ConfigureAwait(false) is not null;

    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cacheKeys);
        var result = new Dictionary<string, T?>(StringComparer.Ordinal);
        if (cacheKeys.Count == 0) return result;

        foreach (var key in cacheKeys)
        {
            result[key] = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) return Task.CompletedTask;

        var entryOptions = new DistributedCacheEntryOptions();
        if (expirationTime > TimeSpan.Zero)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = expirationTime;
        }

        return SetManyAsync(items, entryOptions, cancellationToken);
    }

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) return Task.CompletedTask;

        var tasks = new List<Task>(items.Count);
        foreach (var item in items)
        {
            var entryOptions = BuildEntryOptions(item.Value, options);
            if (entryOptions is null) continue;
            tasks.Add(SetPayloadAsync(item.Key, item.Value, entryOptions, cancellationToken));
        }

        return Task.WhenAll(tasks);
    }

    public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cacheKeys);
        if (cacheKeys.Count == 0) return Task.CompletedTask;

        var tasks = new List<Task>(cacheKeys.Count);
        foreach (var key in cacheKeys)
        {
            tasks.Add(cache.RemoveAsync(key, cancellationToken));
        }

        return Task.WhenAll(tasks);
    }

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        KyrolusCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var payload = await GetPayloadAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (payload is not null)
        {
            if (IsNullPayload(payload)) return default!;
            return JsonSerializer.Deserialize<T>(payload, json)!;
        }

        var value = await factory(cancellationToken).ConfigureAwait(false);
        var entryOptions = BuildEntryOptions(value, options);
        if (entryOptions is null)
        {
            return value;
        }

        await SetPayloadAsync(cacheKey, value, entryOptions, cancellationToken).ConfigureAwait(false);
        return value;
    }

    private async Task<byte[]?> GetPayloadAsync(string cacheKey, CancellationToken cancellationToken)
    {
        return await cache.GetAsync(ValidateKey(cacheKey), cancellationToken).ConfigureAwait(false);
    }

    private Task SetPayloadAsync<T>(string cacheKey, T value, DistributedCacheEntryOptions options, CancellationToken cancellationToken)
    {
        var payload = value is null ? NullPayload : JsonSerializer.SerializeToUtf8Bytes(value, json);
        return cache.SetAsync(ValidateKey(cacheKey), payload, options, cancellationToken);
    }

    private static bool IsNullPayload(byte[] payload)
        => payload.Length == 1 && payload[0] == 0;

    private static string ValidateKey(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            throw new ArgumentException("Cache key is required.", nameof(cacheKey));
        return cacheKey;
    }

    private static DistributedCacheEntryOptions? BuildEntryOptions<T>(T value, KyrolusCacheEntryOptions? options)
    {
        if (options is null) return new DistributedCacheEntryOptions();

        var isNull = value is null;
        var ttl = isNull ? options.NegativeExpirationRelativeToNow : options.AbsoluteExpirationRelativeToNow;
        if (isNull && ttl is null && options.SlidingExpiration is null)
        {
            return null;
        }

        var entryOptions = new DistributedCacheEntryOptions();
        if (ttl is { } resolved && resolved > TimeSpan.Zero)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = ApplyJitter(resolved, options.Jitter);
        }

        if (options.SlidingExpiration is { } sliding && sliding > TimeSpan.Zero)
        {
            entryOptions.SlidingExpiration = ApplyJitter(sliding, options.Jitter);
        }

        return entryOptions;
    }

    private static TimeSpan ApplyJitter(TimeSpan ttl, TimeSpan? jitter)
    {
        if (jitter is null || jitter.Value <= TimeSpan.Zero) return ttl;
        var extraMs = Random.Shared.NextDouble() * jitter.Value.TotalMilliseconds;
        return ttl + TimeSpan.FromMilliseconds(extraMs);
    }

    private Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, DistributedCacheEntryOptions entryOptions, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>(items.Count);
        foreach (var item in items)
        {
            tasks.Add(SetPayloadAsync(item.Key, item.Value, entryOptions, cancellationToken));
        }

        return Task.WhenAll(tasks);
    }
}
