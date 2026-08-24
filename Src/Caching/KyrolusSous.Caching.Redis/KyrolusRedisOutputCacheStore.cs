namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Implements ASP.NET Core 7/8/9/10 <see cref="IOutputCacheStore"/> backed by Redis with atomic Lua tag-based invalidation.
/// </summary>
/// <remarks>
/// <b>Real-World Use Case (Full HTTP Response Output Caching):</b>
/// Allows decorating controller endpoints or Minimal APIs with <c>[OutputCache(Duration = 60, Tags = ["blog"])]</c>.
/// The entire HTTP response (headers, status code, JSON/HTML body) is stored in Redis. 
/// Subsequent HTTP GET requests are served directly from Redis in 1 millisecond without executing controller code or database queries.
/// Calling <c>IOutputCacheStore.EvictByTagAsync("blog")</c> instantly evicts all cached blog pages.
/// </remarks>
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

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusRedisOutputCacheStore"/>.
    /// </summary>
    /// <param name="multiplexer">The active Redis connection multiplexer.</param>
    /// <param name="keyFactory">Optional key factory.</param>
    /// <param name="options">Optional Redis options.</param>
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

    /// <inheritdoc />
    public async ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedKey = keyFactory.BuildKey($"output:{key}");
        var value = await database.StringGetAsync(resolvedKey, options.ReadCommandFlags).ConfigureAwait(false);
        return value.IsNull ? null : (byte[]?)value;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
