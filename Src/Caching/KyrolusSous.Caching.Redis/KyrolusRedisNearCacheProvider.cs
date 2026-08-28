namespace KyrolusSous.Caching.Redis;

/// <summary>
/// High-performance hybrid two-tier cache provider (L1 In-Memory + L2 Distributed Redis) with automatic Pub/Sub synchronization.
/// </summary>
/// <remarks>
/// <b>Real-World Use Case (Sub-microsecond Read Performance):</b>
/// <para>
/// <b>Read Path:</b> Checks L1 local in-process memory first (50 nanoseconds, zero network cost). 
/// On an L1 cache miss, reads from L2 Redis (1-2 ms), populates L1, and returns the result.
/// </para>
/// <para>
/// <b>Write Path:</b> Writes to L2 Redis, updates local L1, and broadcasts an invalidation event across 
/// Redis Pub/Sub so that all other server nodes evict their stale L1 memory immediately.
/// </para>
/// </remarks>
public sealed class KyrolusRedisNearCacheProvider : IKyrolusCacheProvider, IDisposable
{
    private const string L1ProviderName = "redis-near-l1";
    private readonly KyrolusRedisCacheProvider l2;
    private readonly L1CacheStore l1;
    private readonly KyrolusRedisNearCacheOptions options;
    private readonly IKyrolusCacheKeyFactory keyFactory;
    private readonly KyrolusRedisCacheOptions cacheOptions;
    private readonly IKyrolusCacheObserver observer;
    private readonly IKyrolusCacheInvalidationBus? invalidationBus;
    private readonly IDisposable? subscription;

    public KyrolusRedisNearCacheProvider(
        IMemoryCache memoryCache,
        IConnectionMultiplexer multiplexer,
        KyrolusRedisCacheDependencies cacheDependencies,
        KyrolusRedisNearCacheOptions? nearCacheOptions = null,
        IKyrolusCacheInvalidationBus? invalidationBus = null)
    {
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(multiplexer);
        ArgumentNullException.ThrowIfNull(cacheDependencies);

        options = nearCacheOptions ?? new KyrolusRedisNearCacheOptions();
        keyFactory = cacheDependencies.KeyFactory;
        cacheOptions = cacheDependencies.Options;
        observer = cacheDependencies.Observer;
        l2 = new KyrolusRedisCacheProvider(multiplexer, cacheDependencies);
        l1 = new L1CacheStore(memoryCache, options);

        this.invalidationBus = invalidationBus ?? new KyrolusRedisInvalidationBus(
            multiplexer,
            KyrolusRedisInvalidationOptions.FromNearCacheOptions(options));

        if (options.SubscribeInvalidations)
        {
            subscription = this.invalidationBus.Subscribe(message =>
            {
                HandleInvalidation(message);
                return Task.CompletedTask;
            });
        }
    }

    public async Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
    {
        var resolvedKey = ResolveKey(cacheKey, null);
        var (region, tenantId) = ResolveNamespace(null);
        var sw = Stopwatch.StartNew();
        if (l1.TryGet(resolvedKey, out T? cached))
        {
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Get, L1ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.Get, L1ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(
                Key: cacheKey,
                Operation: KyrolusCacheOperation.Get,
                Observation: KyrolusCacheObservation.Hit,
                ValueType: typeof(T),
                Duration: sw.Elapsed,
                Region: region,
                TenantId: tenantId,
                Exception: null)).ConfigureAwait(false);
            return cached;
        }

        sw.Stop();
        KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Get, L1ProviderName, sw.Elapsed);
        KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.Get, L1ProviderName);
        await ObserveAsync(new KyrolusCacheObserverContext(
            Key: cacheKey,
            Operation: KyrolusCacheOperation.Get,
            Observation: KyrolusCacheObservation.Miss,
            ValueType: typeof(T),
            Duration: sw.Elapsed,
            Region: region,
            TenantId: tenantId,
            Exception: null)).ConfigureAwait(false);
        return await GetAndPopulateAsync<T>(cacheKey, resolvedKey, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        await l2.SetAsync(cacheKey, value, expirationTime, cancellationToken).ConfigureAwait(false);
        var resolvedKey = ResolveKey(cacheKey, null);
        l1.Set(resolvedKey, value, expirationTime, null);
        PublishInvalidation(KyrolusCacheInvalidationKind.Key, resolvedKey);
    }

    public async Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        await l2.SetAsync(cacheKey, value, options, cancellationToken).ConfigureAwait(false);
        var resolvedKey = ResolveKey(cacheKey, options);
        l1.Set(resolvedKey, value, default, BuildL1Options(options));
        PublishInvalidation(KyrolusCacheInvalidationKind.Key, resolvedKey);
    }

    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        await l2.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        var resolvedKey = ResolveKey(cacheKey, null);
        l1.Remove(resolvedKey);
        PublishInvalidation(KyrolusCacheInvalidationKind.Key, resolvedKey);
    }

    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default) =>
        l2.ExistsAsync(cacheKey, cancellationToken);

    public async Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
    {
        await l2.RemoveKeysByPatternAsync(keyPattern, cancellationToken).ConfigureAwait(false);
        var resolvedPattern = ResolveKey(keyPattern, null);
        l1.RemoveByPattern(resolvedPattern);
        PublishInvalidation(KyrolusCacheInvalidationKind.Pattern, resolvedPattern);
    }

    public async Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        if (cacheKeys.Count == 0)
        {
            return new Dictionary<string, T?>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, T?>(cacheKeys.Count, StringComparer.Ordinal);
        var misses = new List<string>();
        var resolvedMap = new Dictionary<string, string>(cacheKeys.Count, StringComparer.Ordinal);
        long hits = 0;
        long missCount = 0;

        foreach (var key in cacheKeys)
        {
            var resolvedKey = ResolveKey(key, null);
            resolvedMap[key] = resolvedKey;
            if (l1.TryGet(resolvedKey, out T? cached))
            {
                result[key] = cached;
                hits++;
                continue;
            }

            misses.Add(key);
            missCount++;
        }

        if (hits > 0)
        {
            KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.GetMany, L1ProviderName, hits);
        }

        if (missCount > 0)
        {
            KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.GetMany, L1ProviderName, missCount);
        }

        if (misses.Count == 0)
        {
            return result;
        }

        var fromL2 = await l2.GetManyAsync<T>(misses, cancellationToken).ConfigureAwait(false);
        var defaults = new List<string>();

        foreach (var (key, value) in fromL2)
        {
            result[key] = value;
            if (!EqualityComparer<T>.Default.Equals(value!, default!))
            {
                l1.Set(resolvedMap[key], value!, default, null);
                continue;
            }

            defaults.Add(key);
        }

        if (defaults.Count > 0)
        {
            var existenceTasks = defaults.Select(async key => (Key: key, Exists: await l2.ExistsAsync(key, cancellationToken).ConfigureAwait(false)));
            foreach (var (key, exists) in await Task.WhenAll(existenceTasks).ConfigureAwait(false))
            {
                if (exists)
                {
                    l1.Set(resolvedMap[key], result[key]!, default, null);
                }
            }
        }

        return result;
    }

    public async Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        await l2.SetManyAsync(items, expirationTime, cancellationToken).ConfigureAwait(false);
        var resolved = items.Select(item => new KeyValuePair<string, T>(ResolveKey(item.Key, null), item.Value)).ToArray();
        l1.SetMany(resolved, expirationTime, null);
        PublishInvalidation(KyrolusCacheInvalidationKind.Keys, resolved.Select(item => item.Key));
    }

    public async Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        await l2.SetManyAsync(items, options, cancellationToken).ConfigureAwait(false);
        var l1Options = BuildL1Options(options);
        var resolved = items.Select(item => new KeyValuePair<string, T>(ResolveKey(item.Key, options), item.Value)).ToArray();
        l1.SetMany(resolved, default, l1Options);
        PublishInvalidation(KyrolusCacheInvalidationKind.Keys, resolved.Select(item => item.Key));
    }

    public async Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        await l2.RemoveManyAsync(cacheKeys, cancellationToken).ConfigureAwait(false);
        var resolved = cacheKeys.Select(key => ResolveKey(key, null)).ToArray();
        l1.RemoveMany(resolved);
        PublishInvalidation(KyrolusCacheInvalidationKind.Keys, resolved);
    }

    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await l2.RemoveByTagAsync(tag, cancellationToken).ConfigureAwait(false);
        var resolvedTag = ResolveTag(tag, null);
        l1.RemoveByTag(resolvedTag);
        PublishInvalidation(KyrolusCacheInvalidationKind.Tag, resolvedTag);
    }

    public async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<CancellationToken, Task<T>> factory, KyrolusCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var resolvedKey = ResolveKey(cacheKey, options);
        var (region, tenantId) = ResolveNamespace(options);
        var sw = Stopwatch.StartNew();
        if (l1.TryGet(resolvedKey, out T? cached))
        {
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.GetOrCreate, L1ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.GetOrCreate, L1ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(
                Key: cacheKey,
                Operation: KyrolusCacheOperation.GetOrCreate,
                Observation: KyrolusCacheObservation.Hit,
                ValueType: typeof(T),
                Duration: sw.Elapsed,
                Region: region,
                TenantId: tenantId,
                Exception: null)).ConfigureAwait(false);
            return cached!;
        }

        sw.Stop();
        KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.GetOrCreate, L1ProviderName, sw.Elapsed);
        KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.GetOrCreate, L1ProviderName);
        await ObserveAsync(new KyrolusCacheObserverContext(
            Key: cacheKey,
            Operation: KyrolusCacheOperation.GetOrCreate,
            Observation: KyrolusCacheObservation.Miss,
            ValueType: typeof(T),
            Duration: sw.Elapsed,
            Region: region,
            TenantId: tenantId,
            Exception: null)).ConfigureAwait(false);

        var value = await l2.GetOrCreateAsync(cacheKey, factory, options, cancellationToken).ConfigureAwait(false);
        l1.Set(resolvedKey, value, default, BuildL1Options(options));
        return value;
    }

    public async Task<long> IncrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        var result = await l2.IncrementAsync(cacheKey, value, expirationTime, cancellationToken).ConfigureAwait(false);
        var resolvedKey = ResolveKey(cacheKey, null);
        l1.Remove(resolvedKey);
        PublishInvalidation(KyrolusCacheInvalidationKind.Key, resolvedKey);
        return result;
    }

    public async Task<long> DecrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        var result = await l2.DecrementAsync(cacheKey, value, expirationTime, cancellationToken).ConfigureAwait(false);
        var resolvedKey = ResolveKey(cacheKey, null);
        l1.Remove(resolvedKey);
        PublishInvalidation(KyrolusCacheInvalidationKind.Key, resolvedKey);
        return result;
    }

    public async Task<bool> HashSetAsync<TField>(string cacheKey, string field, TField value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        var result = await l2.HashSetAsync(cacheKey, field, value, expirationTime, cancellationToken).ConfigureAwait(false);
        var resolvedKey = ResolveKey(cacheKey, null);
        l1.Remove(resolvedKey);
        PublishInvalidation(KyrolusCacheInvalidationKind.Key, resolvedKey);
        return result;
    }

    public Task<TField?> HashGetAsync<TField>(string cacheKey, string field, CancellationToken cancellationToken = default) =>
        l2.HashGetAsync<TField>(cacheKey, field, cancellationToken);

    public Task<IDictionary<string, TField?>> HashGetAllAsync<TField>(string cacheKey, CancellationToken cancellationToken = default) =>
        l2.HashGetAllAsync<TField>(cacheKey, cancellationToken);

    public async Task<bool> HashDeleteAsync(string cacheKey, string field, CancellationToken cancellationToken = default)
    {
        var result = await l2.HashDeleteAsync(cacheKey, field, cancellationToken).ConfigureAwait(false);
        var resolvedKey = ResolveKey(cacheKey, null);
        l1.Remove(resolvedKey);
        PublishInvalidation(KyrolusCacheInvalidationKind.Key, resolvedKey);
        return result;
    }

    public void Dispose()
    {
        subscription?.Dispose();
    }

    private async Task<T?> GetAndPopulateAsync<T>(string cacheKey, string resolvedKey, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken)
    {
        var value = await l2.GetAsync<T>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (!EqualityComparer<T>.Default.Equals(value!, default!))
        {
            l1.Set(resolvedKey, value!, default, BuildL1Options(options));
            return value;
        }

        if (await l2.ExistsAsync(cacheKey, cancellationToken).ConfigureAwait(false))
        {
            l1.Set(resolvedKey, value!, default, BuildL1Options(options));
        }

        return value;
    }

    private Task ObserveAsync(KyrolusCacheObserverContext context)
    {
        if (observer is KyrolusNullCacheObserver)
        {
            return Task.CompletedTask;
        }

        return observer.OnObservationAsync(context);
    }

    private void PublishInvalidation(KyrolusCacheInvalidationKind kind, string value)
    {
        if (!options.PublishInvalidations || invalidationBus is null)
        {
            return;
        }

        _ = invalidationBus.PublishAsync(new KyrolusCacheInvalidationMessage(kind, new[] { value }));
    }

    private void PublishInvalidation(KyrolusCacheInvalidationKind kind, IEnumerable<string> values)
    {
        if (!options.PublishInvalidations || invalidationBus is null)
        {
            return;
        }

        var payload = values is IReadOnlyCollection<string> collection ? collection : values.ToArray();
        _ = invalidationBus.PublishAsync(new KyrolusCacheInvalidationMessage(kind, payload));
    }

    private void HandleInvalidation(KyrolusCacheInvalidationMessage message)
    {
        switch (message.Kind)
        {
            case KyrolusCacheInvalidationKind.Key:
                foreach (var value in message.Values)
                {
                    l1.Remove(value);
                }
                break;
            case KyrolusCacheInvalidationKind.Keys:
                l1.RemoveMany(message.Values);
                break;
            case KyrolusCacheInvalidationKind.Tag:
                foreach (var value in message.Values)
                {
                    l1.RemoveByTag(value);
                }
                break;
            case KyrolusCacheInvalidationKind.Pattern:
                foreach (var value in message.Values)
                {
                    l1.RemoveByPattern(value);
                }
                break;
        }
    }

    private string ResolveKey(string cacheKey, KyrolusCacheEntryOptions? entryOptions)
    {
        var (region, tenantId) = ResolveNamespace(entryOptions);
        return keyFactory.BuildKey(cacheKey, region, tenantId);
    }

    private string ResolveTag(string tag, KyrolusCacheEntryOptions? entryOptions)
    {
        var (region, tenantId) = ResolveNamespace(entryOptions);
        return keyFactory.BuildTagKey(tag, region, tenantId);
    }

    private KyrolusCacheEntryOptions? BuildL1Options(KyrolusCacheEntryOptions? entryOptions)
    {
        if (entryOptions is null)
        {
            return null;
        }

        IReadOnlyCollection<string>? tags = entryOptions.Tags;
        if (tags is { Count: > 0 })
        {
            tags = tags.Select(tag => ResolveTag(tag, entryOptions)).ToArray();
        }

        return new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = entryOptions.AbsoluteExpirationRelativeToNow,
            SlidingExpiration = entryOptions.SlidingExpiration,
            Jitter = entryOptions.Jitter,
            NegativeExpirationRelativeToNow = entryOptions.NegativeExpirationRelativeToNow,
            Tags = tags,
            Region = entryOptions.Region,
            TenantId = entryOptions.TenantId
        };
    }

    private (string? Region, string? TenantId) ResolveNamespace(KyrolusCacheEntryOptions? entryOptions)
    {
        var region = entryOptions?.Region ?? cacheOptions.DefaultRegion;
        var tenantId = entryOptions?.TenantId ?? cacheOptions.DefaultTenantId;

        if (cacheOptions.RequireRegion && string.IsNullOrWhiteSpace(region))
        {
            throw new InvalidOperationException("Cache region is required but was not provided.");
        }

        if (cacheOptions.RequireTenantId && string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("Cache tenant id is required but was not provided.");
        }

        return (region, tenantId);
    }

    private sealed class L1CacheStore(IMemoryCache cache, KyrolusRedisNearCacheOptions options)
    {
        private readonly IMemoryCache cache = cache;
        private readonly KyrolusRedisNearCacheOptions options = options;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> tagToKeys = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> keyToTags = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> keys = new(StringComparer.Ordinal);

        public bool TryGet<T>(string key, out T? value)
        {
            if (cache.TryGetValue(key, out var stored))
            {
                if (ReferenceEquals(stored, NullSentinel))
                {
                    value = default;
                    return true;
                }

                if (stored is T typed)
                {
                    value = typed;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void Set<T>(string key, T value, TimeSpan expirationTime, KyrolusCacheEntryOptions? entryOptions)
        {
            var memoryOptions = BuildMemoryOptions(expirationTime, entryOptions);
            cache.Set(key, value is null ? NullSentinel : value, memoryOptions);
            keys[key] = 0;
            if (entryOptions?.Tags is { Count: > 0 } tags)
            {
                TrackTags(key, tags);
            }
        }

        public void SetMany<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime, KyrolusCacheEntryOptions? entryOptions)
        {
            foreach (var item in items)
            {
                Set(item.Key, item.Value, expirationTime, entryOptions);
            }
        }

        public void Remove(string key)
        {
            cache.Remove(key);
            keys.TryRemove(key, out _);
            if (!keyToTags.TryRemove(key, out var tags))
            {
                return;
            }

            foreach (var tag in tags.Keys)
            {
                if (!tagToKeys.TryGetValue(tag, out var tagKeys))
                {
                    continue;
                }

                tagKeys.TryRemove(key, out _);
                if (tagKeys.IsEmpty)
                {
                    tagToKeys.TryRemove(tag, out _);
                }
            }
        }

        public void RemoveMany(IEnumerable<string> cacheKeys)
        {
            foreach (var key in cacheKeys)
            {
                Remove(key);
            }
        }

        public void RemoveByTag(string tag)
        {
            if (!tagToKeys.TryRemove(tag, out var tagKeys))
            {
                return;
            }

            foreach (var key in tagKeys.Keys)
            {
                Remove(key);
            }
        }

        public void RemoveByPattern(string pattern)
        {
            var normalized = NormalizePattern(pattern);
            foreach (var key in keys.Keys)
            {
                if (normalized.Length == 0 || key.Contains(normalized, StringComparison.Ordinal))
                {
                    Remove(key);
                }
            }
        }

        private MemoryCacheEntryOptions BuildMemoryOptions(TimeSpan expirationTime, KyrolusCacheEntryOptions? entryOptions)
        {
            var memoryOptions = new MemoryCacheEntryOptions();
            var sliding = entryOptions?.SlidingExpiration ?? options.DefaultL1SlidingTtl;
            if (sliding.HasValue)
            {
                memoryOptions.SlidingExpiration = ApplyJitter(sliding.Value);
            }

            var absolute = entryOptions?.AbsoluteExpirationRelativeToNow ?? options.DefaultL1Ttl;
            if (!absolute.HasValue && expirationTime != default)
            {
                absolute = expirationTime;
            }

            if (absolute.HasValue)
            {
                memoryOptions.AbsoluteExpirationRelativeToNow = ApplyJitter(absolute.Value);
            }

            return memoryOptions;
        }

        private TimeSpan ApplyJitter(TimeSpan ttl)
        {
            if (options.L1Jitter is null || options.L1Jitter.Value <= TimeSpan.Zero)
            {
                return ttl;
            }

            var max = (int)Math.Max(1, options.L1Jitter.Value.TotalMilliseconds);
            var extra = TimeSpan.FromMilliseconds(Random.Shared.Next(0, max));
            return ttl + extra;
        }

        private void TrackTags(string key, IReadOnlyCollection<string> tags)
        {
            var tagSet = keyToTags.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
            foreach (var tag in tags)
            {
                tagSet[tag] = 0;
                var keysSet = tagToKeys.GetOrAdd(tag, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
                keysSet[key] = 0;
            }
        }

        private static string NormalizePattern(string pattern) =>
            pattern.Trim('*');

        private static readonly object NullSentinel = new();
    }
}
