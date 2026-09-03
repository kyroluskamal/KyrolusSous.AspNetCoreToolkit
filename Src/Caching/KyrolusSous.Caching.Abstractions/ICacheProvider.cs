namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Defines the primary contract for distributed and local caching in the Kyrolus Toolkit.
/// Provides high-performance operations for retrieval, batch storage, tag-based group invalidation, 
/// atomic counters, and Redis Hash structures.
/// </summary>
public interface IKyrolusCacheProvider
{
    /// <summary>
    /// Asynchronously retrieves an item from the cache by its unique key.
    /// </summary>
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously stores an item in the cache with a simple absolute expiration duration.
    /// </summary>
    Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously stores an item in the cache using advanced entry options (sliding expiration, jitter, tags, tenant).
    /// </summary>
    Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a specific item from the cache by its key.
    /// </summary>
    Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines whether a key currently exists in the cache.
    /// </summary>
    Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes all cache entries matching a glob wildcard pattern (e.g., <c>"user:100:*"</c>).
    /// </summary>
    Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves multiple cached items in a single network roundtrip (Redis MGET).
    /// </summary>
    Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously stores multiple items in the cache in a single network roundtrip (Redis MSET).
    /// </summary>
    Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously stores multiple items in the cache with advanced entry options in batch.
    /// </summary>
    Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes multiple items from the cache in a single batch operation.
    /// </summary>
    Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously invalidates and removes all cache entries indexed under a specific logical tag.
    /// </summary>
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves an existing cached item, or executes the provided factory delegate to fetch 
    /// and cache the item if it is missing (Cache-Aside pattern with Thundering Herd protection).
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        KyrolusCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a numerical counter in the cache (Redis INCRBY).
    /// </summary>
    Task<long> IncrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(value);

    /// <summary>
    /// Atomically decrements a numerical counter in the cache (Redis DECRBY).
    /// </summary>
    Task<long> DecrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(-value);

    /// <summary>
    /// Asynchronously sets a field value inside a Redis Hash structure (HSET).
    /// </summary>
    Task<bool> HashSetAsync<TField>(string cacheKey, string field, TField value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <summary>
    /// Asynchronously retrieves a specific field value from a Redis Hash structure (HGET).
    /// </summary>
    Task<TField?> HashGetAsync<TField>(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult<TField?>(default);

    /// <summary>
    /// Asynchronously retrieves all fields and values from a Redis Hash map structure (HGETALL).
    /// </summary>
    Task<IDictionary<string, TField?>> HashGetAllAsync<TField>(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<IDictionary<string, TField?>>(new Dictionary<string, TField?>());

    /// <summary>
    /// Asynchronously deletes a specific field from a Redis Hash map structure (HDEL).
    /// </summary>
    Task<bool> HashDeleteAsync(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <summary>
    /// Atomically stores <paramref name="value"/> under <paramref name="cacheKey"/> only if the key is
    /// not already present, returning whether this call is the one that claimed it.
    /// </summary>
    /// <remarks>
    /// Built for "claim before you execute" patterns such as idempotency keys and distributed locks,
    /// where a plain <c>ExistsAsync</c> then <c>SetAsync</c> leaves a race window between two
    /// concurrent callers that both observe "not present" and both proceed. The default
    /// implementation here is that naive, non-atomic sequence - it is provided so every provider keeps
    /// compiling without a breaking interface change, not because it is safe under real concurrency.
    /// A provider backed by a store that supports a real atomic compare-and-set (Redis <c>SET NX</c>,
    /// for example) should override this with that primitive instead of relying on the default.
    /// </remarks>
    /// <returns><see langword="true"/> if this call created the entry; <see langword="false"/> if the key already existed.</returns>
    async Task<bool> SetIfNotExistsAsync<T>(
        string cacheKey,
        T value,
        KyrolusCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsAsync(cacheKey, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await SetAsync(cacheKey, value, options, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
