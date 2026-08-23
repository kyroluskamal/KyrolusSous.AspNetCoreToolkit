using KyrolusSous.Caching.Abstractions;
using Microsoft.AspNetCore.OutputCaching;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Implements ASP.NET Core <see cref="IOutputCacheStore"/> backed by Redis with tag invalidation support.
/// </summary>
public sealed class KyrolusRedisOutputCacheStore : IOutputCacheStore
{
    private const string TagInvalidationScript =
        "local tagKey = KEYS[1] " +
        "local keys = redis.call('SMEMBERS', tagKey) " +
        "for i=1,#keys do " +
        "  local key = keys[i] " +
        "  local entryTagsKey = key .. ':tags' " +
        "  local tagKeys = redis.call('SMEMBERS', entryTagsKey) " +
        "  for j=1,#tagKeys do " +
        "    redis.call('SREM', tagKeys[j], key) " +
        "  end " +
        "  redis.call('DEL', entryTagsKey) " +
        "  redis.call('DEL', key) " +
        "end " +
        "redis.call('DEL', tagKey) " +
        "return #keys";

    private readonly IConnectionMultiplexer multiplexer;
    private readonly IDatabase database;
    private readonly IKyrolusCacheKeyFactory keyFactory;
    private readonly KyrolusRedisCacheOptions options;

    public KyrolusRedisOutputCacheStore(
        IConnectionMultiplexer multiplexer,
        IKyrolusCacheKeyFactory? keyFactory = null,
        KyrolusRedisCacheOptions? options = null)
    {
        this.multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
        this.database = multiplexer.GetDatabase();
        this.options = options ?? new KyrolusRedisCacheOptions();
        this.keyFactory = keyFactory ?? new KyrolusCacheKeyFactory(this.options.KeyPrefix);
    }

    public async ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedKey = keyFactory.BuildKey($"output:{key}");
        var value = await database.StringGetAsync(resolvedKey, options.ReadCommandFlags).ConfigureAwait(false);
        return value.IsNull ? null : (byte[]?)value;
    }

    public async ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedKey = keyFactory.BuildKey($"output:{key}");
        await database.StringSetAsync(resolvedKey, value, validFor, flags: options.WriteCommandFlags).ConfigureAwait(false);

        if (tags is { Length: > 0 })
        {
            var entryTagsKey = (RedisKey)$"{resolvedKey}:tags";
            foreach (var tag in tags)
            {
                var tagKey = keyFactory.BuildTagKey($"output:tag:{tag}");
                await database.SetAddAsync(tagKey, (RedisValue)resolvedKey.ToString(), options.WriteCommandFlags).ConfigureAwait(false);
                await database.KeyExpireAsync(tagKey, validFor, options.WriteCommandFlags).ConfigureAwait(false);
                await database.SetAddAsync(entryTagsKey, (RedisValue)tagKey.ToString(), options.WriteCommandFlags).ConfigureAwait(false);
            }
            await database.KeyExpireAsync(entryTagsKey, validFor, options.WriteCommandFlags).ConfigureAwait(false);
        }
    }

    public async ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        cancellationToken.ThrowIfCancellationRequested();

        var tagKey = keyFactory.BuildTagKey($"output:tag:{tag}");
        await database.ScriptEvaluateAsync(
            TagInvalidationScript,
            [tagKey],
            [],
            options.WriteCommandFlags).ConfigureAwait(false);
    }
}
