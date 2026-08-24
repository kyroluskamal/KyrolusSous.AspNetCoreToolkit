namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Defines the primary contract for distributed and local caching in the Kyrolus Toolkit.
/// Provides high-performance operations for retrieval, batch storage, tag-based group invalidation, 
/// atomic counters, and Redis Hash structures.
/// </summary>
public interface ICacheProvider
{
    /// <summary>
    /// Asynchronously retrieves an item from the cache by its unique key.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Reading a user profile or blog post by ID: <c>await cache.GetAsync&lt;UserProfile&gt;("user:101")</c>.
    /// </remarks>
    /// <typeparam name="T">The type of the cached object.</typeparam>
    /// <param name="cacheKey">The unique identifier of the cached item.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The cached item, or <c>null</c> if the key does not exist or has expired.</returns>
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously stores an item in the cache with a simple absolute expiration duration.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Caching a weather forecast for 1 hour: <c>await cache.SetAsync("weather:cairo", forecast, TimeSpan.FromHours(1))</c>.
    /// </remarks>
    /// <typeparam name="T">The type of the object to store.</typeparam>
    /// <param name="cacheKey">The unique identifier for the cached item.</param>
    /// <param name="value">The object value to serialize and cache.</param>
    /// <param name="expirationTime">The duration after which the item expires. If <see cref="TimeSpan.Zero"/>, default provider TTL (30 min) is used.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously stores an item in the cache using advanced entry options (sliding expiration, jitter, tags, tenant).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Caching an e-commerce shopping cart with a sliding 15-minute window and tag indexing:
    /// <c>await cache.SetAsync($"cart:{userId}", cart, new KyrolusCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(15), Tags = ["carts"] })</c>.
    /// </remarks>
    /// <typeparam name="T">The type of the object to store.</typeparam>
    /// <param name="cacheKey">The unique identifier for the cached item.</param>
    /// <param name="value">The object value to serialize and cache.</param>
    /// <param name="options">Advanced entry options, or <c>null</c> for defaults.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a specific item from the cache by its key.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// When a user updates their profile picture or password, delete their cached profile immediately 
    /// so the next request reads fresh data from the database: <c>await cache.RemoveAsync("user:101")</c>.
    /// </remarks>
    /// <param name="cacheKey">The unique identifier of the item to remove.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines whether a key currently exists in the cache.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Checking if an active rate-limit or IP blacklist flag exists before proceeding with a request.
    /// </remarks>
    /// <param name="cacheKey">The unique key to check.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns><c>true</c> if the key exists and is unexpired; otherwise, <c>false</c>.</returns>
    Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes all cache entries matching a glob wildcard pattern (e.g., <c>"user:100:*"</c>).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// When user 100 deletes their account, delete all their associated cached keys 
    /// (<c>"user:100:profile"</c>, <c>"user:100:settings"</c>, <c>"user:100:orders"</c>) using pattern <c>"user:100:*"</c>.
    /// </remarks>
    /// <param name="keyPattern">The glob pattern to match keys against.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves multiple cached items in a single network roundtrip (Redis MGET).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Loading the top 50 products on a homepage in a single Redis call instead of making 50 individual roundtrips.
    /// </remarks>
    /// <typeparam name="T">The type of the cached objects.</typeparam>
    /// <param name="cacheKeys">The collection of keys to fetch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A dictionary mapping each key to its cached value (or <c>null</c> if missing).</returns>
    Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously stores multiple items in the cache in a single network roundtrip (Redis MSET).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Pre-warming the cache by bulk inserting 1,000 product categories at startup in a single batch.
    /// </remarks>
    /// <typeparam name="T">The type of the objects to store.</typeparam>
    /// <param name="items">A collection of key-value pairs to store.</param>
    /// <param name="expirationTime">The expiration duration applied to all items in the batch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously stores multiple items in the cache with advanced entry options in batch.
    /// </summary>
    /// <typeparam name="T">The type of the objects to store.</typeparam>
    /// <param name="items">A collection of key-value pairs to store.</param>
    /// <param name="options">Advanced entry options applied to each item in the batch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes multiple items from the cache in a single batch operation.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Bulk deleting a list of 20 expired coupon codes in one command.
    /// </remarks>
    /// <param name="cacheKeys">The collection of keys to remove.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously invalidates and removes all cache entries indexed under a specific logical tag.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Group Invalidation):</b>
    /// When an admin modifies shipping rates for Europe, you call <c>await cache.RemoveByTagAsync("shipping:europe")</c> 
    /// to evict every single country rate cached under that tag across the entire system.
    /// </remarks>
    /// <param name="tag">The logical tag name.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves an existing cached item, or executes the provided factory delegate to fetch 
    /// and cache the item if it is missing (Cache-Aside pattern with Thundering Herd protection).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Protecting Database under Heavy Load):</b>
    /// If 1,000 users simultaneously request the homepage top-sellers list when the cache is empty, 
    /// <c>GetOrCreateAsync</c> ensures that <b>only one single request</b> queries the database, while the other 
    /// 999 requests wait for the result and are served directly from cache once computed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var product = await cache.GetOrCreateAsync(
    ///     $"product:{id}",
    ///     async ct => await dbContext.Products.FindAsync(id, ct),
    ///     new KyrolusCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) },
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        KyrolusCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a numerical counter in the cache (Redis INCRBY).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (API Rate Limiting &amp; Page Views):</b>
    /// Tracking the number of API calls made by a user in the current minute:
    /// <c>long calls = await cache.IncrementAsync($"rate:user:{userId}", 1, TimeSpan.FromMinutes(1));</c>
    /// If <c>calls > 100</c>, you return HTTP 429 Too Many Requests.
    /// </remarks>
    /// <param name="cacheKey">The counter key.</param>
    /// <param name="value">The amount to add (defaults to 1).</param>
    /// <param name="expirationTime">Optional TTL applied to the counter key.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The new counter value after incrementing.</returns>
    Task<long> IncrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(value);

    /// <summary>
    /// Atomically decrements a numerical counter in the cache (Redis DECRBY).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Real-time Inventory Reservations):</b>
    /// Decrementing available flash-sale stock in real-time before committing to the database:
    /// <c>long remainingStock = await cache.DecrementAsync($"stock:ps5_console", 1);</c>
    /// </remarks>
    /// <param name="cacheKey">The counter key.</param>
    /// <param name="value">The amount to subtract (defaults to 1).</param>
    /// <param name="expirationTime">Optional TTL applied to the counter key.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The new counter value after decrementing.</returns>
    Task<long> DecrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(-value);

    /// <summary>
    /// Asynchronously sets a field value inside a Redis Hash structure (HSET).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (User Object Fields / Real-time Settings):</b>
    /// Instead of serializing an entire 50-field user object to change just their status, 
    /// you update a single field in the Redis Hash:
    /// <c>await cache.HashSetAsync("user:101", "status", "Online");</c>
    /// </remarks>
    /// <typeparam name="TField">The type of the field value.</typeparam>
    /// <param name="cacheKey">The key of the Redis Hash map.</param>
    /// <param name="field">The specific field name within the Hash.</param>
    /// <param name="value">The value to store in the field.</param>
    /// <param name="expirationTime">Optional TTL applied to the entire Hash key.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns><c>true</c> if the field was newly created; <c>false</c> if it updated an existing field.</returns>
    Task<bool> HashSetAsync<TField>(string cacheKey, string field, TField value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <summary>
    /// Asynchronously retrieves a specific field value from a Redis Hash structure (HGET).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Reading only the user's role without fetching their full profile:
    /// <c>string? role = await cache.HashGetAsync&lt;string&gt;("user:101", "role");</c>
    /// </remarks>
    /// <typeparam name="TField">The type of the field value.</typeparam>
    /// <param name="cacheKey">The key of the Hash map.</param>
    /// <param name="field">The field name to fetch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The field value, or <c>null</c> if the field or hash is missing.</returns>
    Task<TField?> HashGetAsync<TField>(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult<TField?>(default);

    /// <summary>
    /// Asynchronously retrieves all fields and values from a Redis Hash map structure (HGETALL).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Loading all user permissions or application feature flags stored in a Redis Hash.
    /// </remarks>
    /// <typeparam name="TField">The common type of the field values.</typeparam>
    /// <param name="cacheKey">The key of the Hash map.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A dictionary containing all fields and their values.</returns>
    Task<IDictionary<string, TField?>> HashGetAllAsync<TField>(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<IDictionary<string, TField?>>(new Dictionary<string, TField?>());

    /// <summary>
    /// Asynchronously deletes a specific field from a Redis Hash map structure (HDEL).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Removing a single revoked permission from a user's cached permission map.
    /// </remarks>
    /// <param name="cacheKey">The key of the Hash map.</param>
    /// <param name="field">The field name to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns><c>true</c> if the field existed and was removed; otherwise, <c>false</c>.</returns>
    Task<bool> HashDeleteAsync(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
