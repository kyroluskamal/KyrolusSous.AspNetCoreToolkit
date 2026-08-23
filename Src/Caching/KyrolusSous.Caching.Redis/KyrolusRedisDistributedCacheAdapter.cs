using KyrolusSous.Caching.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Adapts <see cref="RedisCacheProvider"/> to standard ASP.NET Core <see cref="IDistributedCache"/>.
/// </summary>
public sealed class KyrolusRedisDistributedCacheAdapter : IDistributedCache
{
    private const string SlidingSuffix = ":sliding";
    private readonly IConnectionMultiplexer multiplexer;
    private readonly IDatabase database;
    private readonly IKyrolusCacheKeyFactory keyFactory;
    private readonly KyrolusRedisCacheOptions options;

    public KyrolusRedisDistributedCacheAdapter(
        IConnectionMultiplexer multiplexer,
        IKyrolusCacheKeyFactory? keyFactory = null,
        KyrolusRedisCacheOptions? options = null)
    {
        this.multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
        this.database = multiplexer.GetDatabase();
        this.options = options ?? new KyrolusRedisCacheOptions();
        this.keyFactory = keyFactory ?? new KyrolusCacheKeyFactory(this.options.KeyPrefix);
    }

    public byte[]? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var resolvedKey = keyFactory.BuildKey(key);
        var value = database.StringGet(resolvedKey, options.ReadCommandFlags);
        if (value.IsNull) return null;
        RefreshSliding(resolvedKey);
        return (byte[]?)value;
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        token.ThrowIfCancellationRequested();
        var resolvedKey = keyFactory.BuildKey(key);
        var value = await database.StringGetAsync(resolvedKey, options.ReadCommandFlags).ConfigureAwait(false);
        if (value.IsNull) return null;
        await RefreshSlidingAsync(resolvedKey, token).ConfigureAwait(false);
        return (byte[]?)value;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions entryOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(entryOptions);

        var resolvedKey = keyFactory.BuildKey(key);
        var ttl = GetExpiration(entryOptions);

        database.StringSet(resolvedKey, value, ttl, flags: options.WriteCommandFlags);

        if (entryOptions.SlidingExpiration is { } sliding)
        {
            var slidingKey = keyFactory.BuildKey($"{key}{SlidingSuffix}");
            database.StringSet(slidingKey, sliding.Ticks, ttl, flags: options.WriteCommandFlags);
        }
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions entryOptions, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(entryOptions);
        token.ThrowIfCancellationRequested();

        var resolvedKey = keyFactory.BuildKey(key);
        var ttl = GetExpiration(entryOptions);

        await database.StringSetAsync(resolvedKey, value, ttl, flags: options.WriteCommandFlags).ConfigureAwait(false);

        if (entryOptions.SlidingExpiration is { } sliding)
        {
            var slidingKey = keyFactory.BuildKey($"{key}{SlidingSuffix}");
            await database.StringSetAsync(slidingKey, sliding.Ticks, ttl, flags: options.WriteCommandFlags).ConfigureAwait(false);
        }
    }

    public void Refresh(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var resolvedKey = keyFactory.BuildKey(key);
        RefreshSliding(resolvedKey);
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        token.ThrowIfCancellationRequested();
        var resolvedKey = keyFactory.BuildKey(key);
        await RefreshSlidingAsync(resolvedKey, token).ConfigureAwait(false);
    }

    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var resolvedKey = keyFactory.BuildKey(key);
        var slidingKey = keyFactory.BuildKey($"{key}{SlidingSuffix}");
        database.KeyDelete([resolvedKey, slidingKey], options.WriteCommandFlags);
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        token.ThrowIfCancellationRequested();
        var resolvedKey = keyFactory.BuildKey(key);
        var slidingKey = keyFactory.BuildKey($"{key}{SlidingSuffix}");
        await database.KeyDeleteAsync([resolvedKey, slidingKey], options.WriteCommandFlags).ConfigureAwait(false);
    }

    private TimeSpan GetExpiration(DistributedCacheEntryOptions entryOptions)
    {
        if (entryOptions.AbsoluteExpirationRelativeToNow.HasValue)
            return entryOptions.AbsoluteExpirationRelativeToNow.Value;

        if (entryOptions.AbsoluteExpiration.HasValue)
            return entryOptions.AbsoluteExpiration.Value - DateTimeOffset.UtcNow;

        if (entryOptions.SlidingExpiration.HasValue)
            return entryOptions.SlidingExpiration.Value;

        return options.DefaultTtl ?? KyrolusCacheDefaults.DefaultTtl;
    }

    private void RefreshSliding(RedisKey resolvedKey)
    {
        var slidingKey = (RedisKey)$"{resolvedKey}{SlidingSuffix}";
        var slidingVal = database.StringGet(slidingKey, options.ReadCommandFlags);
        if (slidingVal.IsNullOrEmpty) return;
        if (long.TryParse(slidingVal.ToString(), out var ticks))
        {
            var sliding = TimeSpan.FromTicks(ticks);
            database.KeyExpire(resolvedKey, sliding, options.WriteCommandFlags);
            database.KeyExpire(slidingKey, sliding, options.WriteCommandFlags);
        }
    }

    private async Task RefreshSlidingAsync(RedisKey resolvedKey, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var slidingKey = (RedisKey)$"{resolvedKey}{SlidingSuffix}";
        var slidingVal = await database.StringGetAsync(slidingKey, options.ReadCommandFlags).ConfigureAwait(false);
        if (slidingVal.IsNullOrEmpty) return;
        if (long.TryParse(slidingVal.ToString(), out var ticks))
        {
            var sliding = TimeSpan.FromTicks(ticks);
            await database.KeyExpireAsync(resolvedKey, sliding, options.WriteCommandFlags).ConfigureAwait(false);
            await database.KeyExpireAsync(slidingKey, sliding, options.WriteCommandFlags).ConfigureAwait(false);
        }
    }
}
