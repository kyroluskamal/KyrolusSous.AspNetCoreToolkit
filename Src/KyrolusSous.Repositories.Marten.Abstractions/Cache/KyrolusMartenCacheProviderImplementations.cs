using System.Text.Json;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace KyrolusSous.Repositories.Marten.Abstractions.Cache;

public sealed class KyrolusMartenNoopCacheProvider : IKyrolusMartenCacheProvider
{
    public static readonly IKyrolusMartenCacheProvider Instance = new KyrolusMartenNoopCacheProvider();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class KyrolusMartenMemoryCacheProvider : IKyrolusMartenCacheProvider
{
    private readonly IMemoryCache cache;

    public KyrolusMartenMemoryCacheProvider(IMemoryCache cache)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(cache.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }
}

public sealed class KyrolusMartenDistributedCacheProvider : IKyrolusMartenCacheProvider
{
    private readonly IDistributedCache cache;
    private readonly JsonSerializerOptions json;

    public KyrolusMartenDistributedCacheProvider(IDistributedCache cache, JsonSerializerOptions? options = null)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        json = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, json);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, json);
        await cache.SetAsync(
            key,
            bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken).ConfigureAwait(false);
    }

    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(key, cancellationToken);
}
