namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Production-grade distributed caching provider backed by StackExchange.Redis.
/// Supports batch operations, Tag Sets, sliding expiration, Jitter, negative caching, 
/// distributed locking, Lua atomic invalidations, OpenTelemetry metrics, and Circuit Breaker resilience.
/// </summary>
public sealed class RedisCacheProvider : ICacheProvider
{
    private const string ReleaseLockScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
    private const string AcquireLockScript =
        "if redis.call('exists', KEYS[1]) == 0 then redis.call('psetex', KEYS[1], ARGV[2], ARGV[1]); return 1 else return 0 end";
    private const string TagInvalidationScript =
        "local tagKey = KEYS[1] " +
        "local indexKey = KEYS[2] " +
        "local entryTagsSuffix = ARGV[1] " +
        "local slidingSuffix = ARGV[2] " +
        "local negativeSuffix = ARGV[3] " +
        "local keys = redis.call('SMEMBERS', tagKey) " +
        "for i=1,#keys do " +
        "  local key = keys[i] " +
        "  if indexKey and indexKey ~= '' then redis.call('SREM', indexKey, key) end " +
        "  local entryTagsKey = key .. entryTagsSuffix " +
        "  local tagKeys = redis.call('SMEMBERS', entryTagsKey) " +
        "  for j=1,#tagKeys do " +
        "    redis.call('SREM', tagKeys[j], key) " +
        "  end " +
        "  redis.call('DEL', entryTagsKey) " +
        "  redis.call('DEL', key .. slidingSuffix) " +
        "  redis.call('DEL', key .. negativeSuffix) " +
        "  redis.call('DEL', key) " +
        "end " +
        "redis.call('DEL', tagKey) " +
        "return #keys";
    private const string EntryTagsSuffix = ":tags";
    private const string SlidingSuffix = ":sliding";
    private const string LockSuffix = ":lock";
    private const string NegativeSuffix = ":neg";
    private const string ManyKey = "[many]";
    private const int FallbackBatchSize = 256;
    private const string ProviderName = "redis";
    private static readonly InvalidOperationException RedisUnavailableException =
        new("Redis connection is not available.");

    private readonly IConnectionMultiplexer multiplexer;
    private readonly IDatabase database;
    private readonly IKyrolusCacheSerializer serializer;
    private readonly IKyrolusCacheKeyFactory keyFactory;
    private readonly KyrolusRedisCacheOptions options;
    private readonly RedisKey keyIndexKey;
    private readonly RedisKey configSignatureKey;
    private readonly KyrolusRedisPatternRemovalStrategy patternRemovalStrategy;
    private readonly KyrolusRedisLockStrategy lockStrategy;
    private readonly IKyrolusCacheObserver observer;
    private readonly IKyrolusCachePolicyProvider policyProvider;
    private readonly KyrolusRedisCircuitBreaker circuitBreaker;
    private readonly CommandFlags readFlags;
    private readonly CommandFlags writeFlags;

    public RedisCacheProvider(IConnectionMultiplexer multiplexer, KyrolusRedisCacheDependencies? dependencies = null)
    {
        this.multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
        database = this.multiplexer.GetDatabase();

        dependencies ??= KyrolusRedisCacheDependencies.Default;
        serializer = dependencies.Serializer;
        keyFactory = dependencies.KeyFactory;
        options = dependencies.Options;
        KyrolusRedisCacheOptionsValidator.Validate(options);
        patternRemovalStrategy = options.PatternRemovalStrategy;
        keyIndexKey = keyFactory.BuildKey(options.KeyIndexKey);
        configSignatureKey = keyFactory.BuildKey(options.ConfigSignatureKey);
        lockStrategy = options.LockStrategy;
        observer = dependencies.Observer;
        policyProvider = dependencies.PolicyProvider;
        circuitBreaker = new KyrolusRedisCircuitBreaker(options.CircuitBreaker);
        readFlags = options.ReadCommandFlags;
        writeFlags = options.WriteCommandFlags;

        WarnOnConfigurationChange();
    }

    public async Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
    {
        var entryOptions = ApplyPolicy<T>(null, KyrolusCacheOperation.Get);
        var (region, tenantId) = ResolveNamespace(entryOptions);
        var resolvedKey = keyFactory.BuildKey(cacheKey, region, tenantId);
        var negativeTtl = ResolveNegativeTtl(entryOptions);
        var useNegativeCache = ShouldUseNegativeCache<T>(negativeTtl);
        using var activity = StartActivity(KyrolusCacheOperation.Get, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Get, cacheKey, typeof(T), region, tenantId).ConfigureAwait(false))
            return default;
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RedisValue value;
            RedisValue negativeValue = RedisValue.Null;

            if (useNegativeCache)
            {
                var values = await database.StringGetAsync(
                        [resolvedKey, BuildNegativeKey(resolvedKey)],
                        readFlags)
                    .ConfigureAwait(false);
                value = values[0];
                negativeValue = values[1];
            }
            else
            {
                value = await database.StringGetAsync(resolvedKey, readFlags).ConfigureAwait(false);
            }

            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Get, ProviderName, sw.Elapsed);

            if (value.IsNull)
            {
                if (useNegativeCache && !negativeValue.IsNull)
                {
                    KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.Get, ProviderName);
                    await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Get, Observation: KyrolusCacheObservation.Miss, ValueType: typeof(T), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
                    return default;
                }

                KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.Get, ProviderName);
                await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Get, Observation: KyrolusCacheObservation.Miss, ValueType: typeof(T), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
                if (useNegativeCache && negativeTtl.HasValue)
                {
                    await database.StringSetAsync(
                            BuildNegativeKey(resolvedKey),
                            (RedisValue)"1",
                            negativeTtl,
                            When.NotExists,
                            flags: writeFlags)
                        .ConfigureAwait(false);
                    await TrackKeyAsync(resolvedKey).ConfigureAwait(false);
                }
                return default;
            }
            KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.Get, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Get, Observation: KyrolusCacheObservation.Hit, ValueType: typeof(T), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
            await RefreshSlidingAsync(resolvedKey, cancellationToken).ConfigureAwait(false);
            return serializer.Deserialize<T>(value!);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Get, cacheKey, typeof(T), region, tenantId, sw).ConfigureAwait(false);
            return default;
        }
    }

    public async Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        var entryOptions = ApplyPolicy<T>(null, KyrolusCacheOperation.Set);
        var (region, tenantId) = ResolveNamespace(entryOptions);
        var ttl = expirationTime == default
            ? ResolveExpiration(null, entryOptions)
            : ResolveExpiration(expirationTime, entryOptions);
        var resolvedKey = keyFactory.BuildKey(cacheKey, region, tenantId);
        using var activity = StartActivity(KyrolusCacheOperation.Set, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Set, cacheKey, typeof(T), region, tenantId).ConfigureAwait(false))
            return;
        var sw = Stopwatch.StartNew();
        try
        {
            await SetInternalAsync(resolvedKey, value, ttl, entryOptions, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Set, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.Set, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Set, Observation: KyrolusCacheObservation.Set, ValueType: typeof(T), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Set, cacheKey, typeof(T), region, tenantId, sw).ConfigureAwait(false);
        }
    }

    public async Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? requestOptions, CancellationToken cancellationToken = default)
    {
        var entryOptions = ApplyPolicy<T>(requestOptions, KyrolusCacheOperation.Set);
        var (region, tenantId) = ResolveNamespace(entryOptions);
        var ttl = ResolveExpiration(null, entryOptions);
        var resolvedKey = keyFactory.BuildKey(cacheKey, region, tenantId);
        using var activity = StartActivity(KyrolusCacheOperation.Set, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Set, cacheKey, typeof(T), region, tenantId).ConfigureAwait(false))
            return;
        var sw = Stopwatch.StartNew();

        try
        {
            await SetInternalAsync(resolvedKey, value, ttl, entryOptions, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Set, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.Set, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Set, Observation: KyrolusCacheObservation.Set, ValueType: typeof(T), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Set, cacheKey, typeof(T), region, tenantId, sw).ConfigureAwait(false);
        }
    }

    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var (region, tenantId) = ResolveNamespace(null);
        var resolvedKey = ResolveKey(cacheKey, null);
        using var activity = StartActivity(KyrolusCacheOperation.Remove, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Remove, cacheKey, null, region, tenantId).ConfigureAwait(false))
            return;
        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RemoveInternalAsync(resolvedKey, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Remove, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordRemove(KyrolusCacheOperation.Remove, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Remove, Observation: KyrolusCacheObservation.Remove, ValueType: null, Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Remove, cacheKey, null, region, tenantId, sw).ConfigureAwait(false);
        }
    }

    public async Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var (region, tenantId) = ResolveNamespace(null);
        var resolvedKey = ResolveKey(cacheKey, null);
        using var activity = StartActivity(KyrolusCacheOperation.Exists, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Exists, cacheKey, null, region, tenantId).ConfigureAwait(false))
            return false;
        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exists = await database.KeyExistsAsync(resolvedKey, readFlags).ConfigureAwait(false);

            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Exists, ProviderName, sw.Elapsed);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Exists, Observation: KyrolusCacheObservation.Exists, ValueType: null, Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
            return exists;
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Exists, cacheKey, null, region, tenantId, sw).ConfigureAwait(false);
            return false;
        }
    }

    public async Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
    {
        if (patternRemovalStrategy == KyrolusRedisPatternRemovalStrategy.Disabled)
            return;

        var (region, tenantId) = ResolveNamespace(null);
        var resolvedPattern = keyFactory.BuildKey(keyPattern, region, tenantId);
        using var activity = StartActivity(KyrolusCacheOperation.RemoveByPattern, keyPattern, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.RemoveByPattern, keyPattern, null, region, tenantId).ConfigureAwait(false))
            return;
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (patternRemovalStrategy)
            {
                case KyrolusRedisPatternRemovalStrategy.KeyIndex:
                    await RemoveByKeyIndexAsync(resolvedPattern, cancellationToken).ConfigureAwait(false);
                    break;
                case KyrolusRedisPatternRemovalStrategy.ServerScan:
                    await RemoveByServerScanAsync(resolvedPattern, cancellationToken).ConfigureAwait(false);
                    break;
            }

            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.RemoveByPattern, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordRemove(KyrolusCacheOperation.RemoveByPattern, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: keyPattern, Operation: KyrolusCacheOperation.RemoveByPattern, Observation: KyrolusCacheObservation.Remove, ValueType: null, Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.RemoveByPattern, keyPattern, null, region, tenantId, sw).ConfigureAwait(false);
        }
    }

    public async Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        if (cacheKeys.Count == 0)
            return new Dictionary<string, T?>(StringComparer.Ordinal);

        var entryOptions = ApplyPolicy<T>(null, KyrolusCacheOperation.GetMany);
        var (region, tenantId) = ResolveNamespace(entryOptions);
        var negativeTtl = ResolveNegativeTtl(entryOptions);
        var useNegativeCache = ShouldUseNegativeCache<T>(negativeTtl);
        using var activity = StartActivity(KyrolusCacheOperation.GetMany, ManyKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.GetMany, ManyKey, typeof(T), region, tenantId).ConfigureAwait(false))
            return cacheKeys.ToDictionary(key => key, _ => default(T), StringComparer.Ordinal);
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keysArray = cacheKeys as string[] ?? cacheKeys.ToArray();
            var accumulator = await GetManyCoreAsync<T>(
                keysArray,
                entryOptions,
                useNegativeCache,
                cancellationToken).ConfigureAwait(false);

            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.GetMany, ProviderName, sw.Elapsed);
            if (accumulator.Hits > 0)
            {
                KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.GetMany, ProviderName, accumulator.Hits);
                await ObserveAsync(new KyrolusCacheObserverContext(
                    Key: ManyKey,
                    Operation: KyrolusCacheOperation.GetMany,
                    Observation: KyrolusCacheObservation.Hit,
                    ValueType: typeof(T),
                    Duration: sw.Elapsed,
                    Region: region,
                    TenantId: tenantId,
                    Exception: null)).ConfigureAwait(false);
            }
            if (accumulator.Misses > 0)
            {
                KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.GetMany, ProviderName, accumulator.Misses);
                await ObserveAsync(new KyrolusCacheObserverContext(
                    Key: ManyKey,
                    Operation: KyrolusCacheOperation.GetMany,
                    Observation: KyrolusCacheObservation.Miss,
                    ValueType: typeof(T),
                    Duration: sw.Elapsed,
                    Region: region,
                    TenantId: tenantId,
                    Exception: null)).ConfigureAwait(false);
            }

            return accumulator.Result;
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.GetMany, ManyKey, typeof(T), region, tenantId, sw).ConfigureAwait(false);
            return new Dictionary<string, T?>(StringComparer.Ordinal);
        }
    }

    public async Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return;
        var entryOptions = ApplyPolicy<T>(null, KyrolusCacheOperation.SetMany);
        var (region, tenantId) = ResolveNamespace(entryOptions);
        var ttl = ResolveExpiration(expirationTime, entryOptions);
        using var activity = StartActivity(KyrolusCacheOperation.SetMany, ManyKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.SetMany, ManyKey, typeof(T), region, tenantId).ConfigureAwait(false))
            return;
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchSize = GetBatchSize();
            var entries = items as KeyValuePair<string, T>[] ?? items.ToArray();

            foreach (var chunk in entries.Chunk(batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pairs = new KeyValuePair<RedisKey, RedisValue>[chunk.Length];
                for (var index = 0; index < chunk.Length; index++)
                {
                    pairs[index] = new KeyValuePair<RedisKey, RedisValue>(
                        ResolveKey(chunk[index].Key, entryOptions),
                        serializer.Serialize(chunk[index].Value));
                }

                await database.StringSetAsync(pairs, flags: writeFlags).ConfigureAwait(false);
                var negativeKeys = pairs.Select(pair => BuildNegativeKey(pair.Key)).ToArray();
                await database.KeyDeleteAsync(negativeKeys, writeFlags).ConfigureAwait(false);
                await TrackKeysAsync(pairs.Select(p => p.Key)).ConfigureAwait(false);
                await ApplyExpiryAsync(pairs.Select(p => p.Key), ttl).ConfigureAwait(false);
            }

            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.SetMany, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.SetMany, ProviderName, items.Count);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: ManyKey, Operation: KyrolusCacheOperation.SetMany, Observation: KyrolusCacheObservation.Set, ValueType: typeof(T), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.SetMany, ManyKey, typeof(T), region, tenantId, sw).ConfigureAwait(false);
        }
    }

    public async Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? requestOptions, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return;
        var entryOptions = ApplyPolicy<T>(requestOptions, KyrolusCacheOperation.SetMany);
        var (region, tenantId) = ResolveNamespace(entryOptions);
        var ttl = ResolveExpiration(null, entryOptions);
        using var activity = StartActivity(KyrolusCacheOperation.SetMany, ManyKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.SetMany, ManyKey, typeof(T), region, tenantId).ConfigureAwait(false))
        {
            return;
        }
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchSize = GetBatchSize();
            var entries = items as KeyValuePair<string, T>[] ?? items.ToArray();
            foreach (var chunk in entries.Chunk(batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pairs = new KeyValuePair<RedisKey, RedisValue>[chunk.Length];
                for (var index = 0; index < chunk.Length; index++)
                {
                    pairs[index] = new KeyValuePair<RedisKey, RedisValue>(
                        ResolveKey(chunk[index].Key, entryOptions),
                        serializer.Serialize(chunk[index].Value));
                }

                await database.StringSetAsync(pairs, flags: writeFlags).ConfigureAwait(false);
                var negativeKeys = pairs.Select(pair => BuildNegativeKey(pair.Key)).ToArray();
                await database.KeyDeleteAsync(negativeKeys, writeFlags).ConfigureAwait(false);
                await TrackKeysAsync(pairs.Select(p => p.Key)).ConfigureAwait(false);
                await ApplyExpiryAsync(pairs.Select(p => p.Key), ttl).ConfigureAwait(false);
                if (entryOptions?.Tags is { Count: > 0 } tags)
                {
                    await ApplyTagsAsync(pairs.Select(p => p.Key), tags, entryOptions, ttl).ConfigureAwait(false);
                }
            }
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.SetMany, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.SetMany, ProviderName, items.Count);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: ManyKey, Operation: KyrolusCacheOperation.SetMany, Observation: KyrolusCacheObservation.Set, ValueType: typeof(T), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.SetMany, ManyKey, typeof(T), region, tenantId, sw).ConfigureAwait(false);
        }
    }

    public async Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        if (cacheKeys.Count == 0) return;
        var (region, tenantId) = ResolveNamespace(null);
        using var activity = StartActivity(KyrolusCacheOperation.RemoveMany, ManyKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.RemoveMany, ManyKey, null, region, tenantId).ConfigureAwait(false))
            return;
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchSize = GetBatchSize();
            var keysArray = cacheKeys as string[] ?? cacheKeys.ToArray();
            foreach (var chunk in keysArray.Chunk(batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tasks = chunk
                    .Select(key => RemoveInternalAsync(ResolveKey(key, null), cancellationToken))
                    .ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.RemoveMany, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordRemove(KyrolusCacheOperation.RemoveMany, ProviderName, cacheKeys.Count);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: ManyKey, Operation: KyrolusCacheOperation.RemoveMany, Observation: KyrolusCacheObservation.Remove, ValueType: null, Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.RemoveMany, ManyKey, null, region, tenantId, sw).ConfigureAwait(false);
        }
    }

    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var (region, tenantId) = ResolveNamespace(null);
        var tagKey = ResolveTagKey(tag, null);
        var indexKey = patternRemovalStrategy == KyrolusRedisPatternRemovalStrategy.KeyIndex
            ? keyIndexKey
            : (RedisKey)string.Empty;
        using var activity = StartActivity(KyrolusCacheOperation.RemoveByTag, tag, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.RemoveByTag, tag, null, region, tenantId).ConfigureAwait(false))
            return;
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await database.ScriptEvaluateAsync(
                    TagInvalidationScript,
                    [tagKey, indexKey],
                    [EntryTagsSuffix, SlidingSuffix, NegativeSuffix],
                    writeFlags)
                .ConfigureAwait(false);

            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.RemoveByTag, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordRemove(KyrolusCacheOperation.RemoveByTag, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: tag, Operation: KyrolusCacheOperation.RemoveByTag, Observation: KyrolusCacheObservation.Remove, ValueType: null, Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.RemoveByTag, tag, null, region, tenantId, sw).ConfigureAwait(false);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<CancellationToken, Task<T>> factory, KyrolusCacheEntryOptions? requestOptions = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var entryOptions = ApplyPolicy<T>(requestOptions, KyrolusCacheOperation.GetOrCreate);
        var (region, tenantId) = ResolveNamespace(entryOptions);
        var resolvedKey = ResolveKey(cacheKey, entryOptions);
        var negativeTtl = ResolveNegativeTtl(entryOptions);
        var useNegativeCache = ShouldUseNegativeCache<T>(negativeTtl);
        using var activity = StartActivity(KyrolusCacheOperation.GetOrCreate, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.GetOrCreate, cacheKey, typeof(T), region, tenantId).ConfigureAwait(false))
            return await factory(cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = new GetOrCreateState<T>
            {
                CacheKey = cacheKey,
                ResolvedKey = resolvedKey,
                EntryOptions = entryOptions,
                UseNegativeCache = useNegativeCache,
                NegativeTtl = negativeTtl,
                Region = region,
                TenantId = tenantId,
                Factory = factory
            };
            var read = await TryReadCacheAsync(state, cancellationToken).ConfigureAwait(false);
            if (read.HasValue)
                return await HandleCacheHitAsync(state, read.Value!, sw, cancellationToken).ConfigureAwait(false);
            if (read.HasNegative) return await HandleNegativeHitAsync(state, sw).ConfigureAwait(false);
            KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.GetOrCreate, ProviderName);
            await ObserveGetOrCreateAsync(state, KyrolusCacheObservation.Miss, null, null).ConfigureAwait(false);
            if (lockStrategy == KyrolusRedisLockStrategy.Disabled)
                return await CreateWithoutLockAsync(state, sw, cancellationToken).ConfigureAwait(false);
            return await CreateWithLockAsync(state, sw, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.GetOrCreate, cacheKey, typeof(T), region, tenantId, sw).ConfigureAwait(false);
            return await factory(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record GetManyEntry(string Key, RedisKey ResolvedKey);

    private sealed class GetManyAccumulator<T>(int capacity)
    {
        public IDictionary<string, T?> Result { get; } = new Dictionary<string, T?>(capacity, StringComparer.Ordinal);
        public long Hits { get; private set; }
        public long Misses { get; private set; }

        public void AddHit(string key, T? value)
        {
            Result[key] = value;
            Hits++;
        }

        public void AddMiss(string key)
        {
            Result[key] = default;
            Misses++;
        }
    }

    private async Task<GetManyAccumulator<T>> GetManyCoreAsync<T>(
        string[] cacheKeys,
        KyrolusCacheEntryOptions? entryOptions,
        bool useNegativeCache,
        CancellationToken cancellationToken)
    {
        var accumulator = new GetManyAccumulator<T>(cacheKeys.Length);
        if (cacheKeys.Length == 0) return accumulator;
        var batchSize = GetBatchSize();
        foreach (var chunk in cacheKeys.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = BuildGetManyEntries(chunk, entryOptions);
            var resolvedKeys = entries.Select(entry => entry.ResolvedKey).ToArray();
            var (values, negatives) = await FetchManyValuesAsync(resolvedKeys, useNegativeCache).ConfigureAwait(false);
            await ProcessGetManyChunkAsync(entries, values, negatives, useNegativeCache, accumulator, cancellationToken).ConfigureAwait(false);
        }
        return accumulator;
    }

    private GetManyEntry[] BuildGetManyEntries(string[] cacheKeys, KyrolusCacheEntryOptions? entryOptions)
    {
        var entries = new GetManyEntry[cacheKeys.Length];
        for (var index = 0; index < cacheKeys.Length; index++)
            entries[index] = new GetManyEntry(cacheKeys[index], ResolveKey(cacheKeys[index], entryOptions));
        return entries;
    }

    private async Task<(RedisValue[] Values, RedisValue[] Negatives)> FetchManyValuesAsync(RedisKey[] resolvedKeys, bool useNegativeCache)
    {
        if (resolvedKeys.Length == 0) return (Array.Empty<RedisValue>(), Array.Empty<RedisValue>());
        if (!useNegativeCache)
        {
            var values = await database.StringGetAsync(resolvedKeys, readFlags).ConfigureAwait(false);
            return (values, Array.Empty<RedisValue>());
        }
        var pairKeys = new RedisKey[resolvedKeys.Length * 2];
        for (var index = 0; index < resolvedKeys.Length; index++)
        {
            pairKeys[index] = resolvedKeys[index];
            pairKeys[index + resolvedKeys.Length] = BuildNegativeKey(resolvedKeys[index]);
        }
        var combined = await database.StringGetAsync(pairKeys, readFlags).ConfigureAwait(false);
        var valuesResult = new RedisValue[resolvedKeys.Length];
        var negativeResult = new RedisValue[resolvedKeys.Length];
        Array.Copy(combined, 0, valuesResult, 0, resolvedKeys.Length);
        Array.Copy(combined, resolvedKeys.Length, negativeResult, 0, resolvedKeys.Length);
        return (valuesResult, negativeResult);
    }

    private async Task ProcessGetManyChunkAsync<T>(
        GetManyEntry[] entries,
        RedisValue[] values,
        RedisValue[] negatives,
        bool useNegativeCache,
        GetManyAccumulator<T> accumulator,
        CancellationToken cancellationToken)
    {
        var refreshTasks = new List<Task>();
        for (var index = 0; index < entries.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = values[index];
            if (!value.IsNull)
            {
                var typed = serializer.Deserialize<T>(value!)!;
                accumulator.AddHit(entries[index].Key, typed);
                refreshTasks.Add(RefreshSlidingAsync(entries[index].ResolvedKey, cancellationToken));
                continue;
            }
            if (useNegativeCache && index < negatives.Length && !negatives[index].IsNull)
            {
                accumulator.AddMiss(entries[index].Key);
                continue;
            }
            accumulator.AddMiss(entries[index].Key);
        }

        if (refreshTasks.Count > 0) await Task.WhenAll(refreshTasks).ConfigureAwait(false);
    }

    private sealed class GetOrCreateState<T>
    {
        public string CacheKey { get; init; } = string.Empty;
        public RedisKey ResolvedKey { get; init; }
        public KyrolusCacheEntryOptions? EntryOptions { get; init; }
        public bool UseNegativeCache { get; init; }
        public TimeSpan? NegativeTtl { get; init; }
        public string? Region { get; init; }
        public string? TenantId { get; init; }
        public Func<CancellationToken, Task<T>> Factory { get; init; } = _ => Task.FromResult(default(T)!);
    }

    private sealed record CacheReadResult<T>(bool HasValue, bool HasNegative, T? Value);

    private async Task<CacheReadResult<T>> TryReadCacheAsync<T>(GetOrCreateState<T> state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value;
        RedisValue negativeValue = RedisValue.Null;

        if (state.UseNegativeCache)
        {
            var values = await database.StringGetAsync(
                    [state.ResolvedKey, BuildNegativeKey(state.ResolvedKey)],
                    readFlags)
                .ConfigureAwait(false);
            value = values[0];
            negativeValue = values[1];
        }
        else
        {
            value = await database.StringGetAsync(state.ResolvedKey, readFlags).ConfigureAwait(false);
        }
        if (!value.IsNull)
        {
            var typed = serializer.Deserialize<T>(value!)!;
            return new CacheReadResult<T>(true, false, typed);
        }
        if (state.UseNegativeCache && !negativeValue.IsNull)
            return new CacheReadResult<T>(false, true, default);
        return new CacheReadResult<T>(false, false, default);
    }

    private async Task<T> HandleCacheHitAsync<T>(GetOrCreateState<T> state, T value, Stopwatch sw, CancellationToken cancellationToken)
    {
        if (sw.IsRunning) sw.Stop();
        KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.GetOrCreate, ProviderName, sw.Elapsed);
        KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.GetOrCreate, ProviderName);
        await ObserveGetOrCreateAsync(state, KyrolusCacheObservation.Hit, sw.Elapsed, null).ConfigureAwait(false);
        await RefreshSlidingAsync(state.ResolvedKey, cancellationToken).ConfigureAwait(false);
        return value;
    }

    private async Task<T> HandleNegativeHitAsync<T>(GetOrCreateState<T> state, Stopwatch sw)
    {
        if (sw.IsRunning) sw.Stop();
        KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.GetOrCreate, ProviderName, sw.Elapsed);
        KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.GetOrCreate, ProviderName);
        await ObserveGetOrCreateAsync(state, KyrolusCacheObservation.Miss, sw.Elapsed, null).ConfigureAwait(false);
        return default!;
    }

    private Task ObserveGetOrCreateAsync<T>(GetOrCreateState<T> state, KyrolusCacheObservation observation, TimeSpan? duration, Exception? exception)
    {
        return ObserveAsync(new KyrolusCacheObserverContext(
            Key: state.CacheKey,
            Operation: KyrolusCacheOperation.GetOrCreate,
            Observation: observation,
            ValueType: typeof(T),
            Duration: duration,
            Region: state.Region,
            TenantId: state.TenantId,
            Exception: exception));
    }

    private async Task<T> CreateWithoutLockAsync<T>(GetOrCreateState<T> state, Stopwatch sw, CancellationToken cancellationToken)
    {
        var created = await state.Factory(cancellationToken).ConfigureAwait(false);
        await StoreOrNegativeAsync(state.ResolvedKey, created, state.EntryOptions, state.UseNegativeCache, state.NegativeTtl, cancellationToken).ConfigureAwait(false);
        return await FinalizeSetAsync(state, created, sw).ConfigureAwait(false);
    }

    private async Task<T> CreateWithLockAsync<T>(GetOrCreateState<T> state, Stopwatch sw, CancellationToken cancellationToken)
    {
        var lockKey = BuildLockKey(state.ResolvedKey);
        var waitUntil = DateTimeOffset.UtcNow + (options.LockWait ?? KyrolusCacheDefaults.DefaultLockWait);
        var waitSw = Stopwatch.StartNew();
        var attempt = 0;

        while (DateTimeOffset.UtcNow < waitUntil)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;
            var lockToken = await TryAcquireLockAsync(lockKey, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(lockToken))
            {
                KyrolusCacheInstrumentation.RecordLockAcquired(ProviderName);
                await ObserveGetOrCreateAsync(state, KyrolusCacheObservation.LockAcquired, null, null).ConfigureAwait(false);
                RecordLockWait(waitSw);

                try
                {
                    var created = await state.Factory(cancellationToken).ConfigureAwait(false);
                    await StoreOrNegativeAsync(state.ResolvedKey, created, state.EntryOptions, state.UseNegativeCache, state.NegativeTtl, cancellationToken).ConfigureAwait(false);
                    return await FinalizeSetAsync(state, created, sw).ConfigureAwait(false);
                }
                finally
                {
                    await ReleaseLockAsync(lockKey, lockToken).ConfigureAwait(false);
                }
            }
            KyrolusCacheInstrumentation.RecordLockFailed(ProviderName);
            await ObserveGetOrCreateAsync(state, KyrolusCacheObservation.LockFailed, null, null).ConfigureAwait(false);
            var cached = await TryReadAfterLockAsync(state, cancellationToken).ConfigureAwait(false);
            if (cached.HasValue)
            {
                RecordLockWait(waitSw);
                return await HandleCacheHitAsync(state, cached.Value!, sw, cancellationToken).ConfigureAwait(false);
            }
            var retryDelay = GetLockRetryDelay(attempt);
            if (retryDelay > TimeSpan.Zero)
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
        }
        RecordLockWait(waitSw);
        var fallback = await state.Factory(cancellationToken).ConfigureAwait(false);
        await StoreOrNegativeAsync(state.ResolvedKey, fallback, state.EntryOptions, state.UseNegativeCache, state.NegativeTtl, cancellationToken).ConfigureAwait(false);
        return await FinalizeSetAsync(state, fallback, sw).ConfigureAwait(false);
    }

    private async Task<CacheReadResult<T>> TryReadAfterLockAsync<T>(GetOrCreateState<T> state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await database.StringGetAsync(state.ResolvedKey, readFlags).ConfigureAwait(false);
        if (value.IsNull)
            return new CacheReadResult<T>(false, false, default);
        var typed = serializer.Deserialize<T>(value!)!;
        return new CacheReadResult<T>(true, false, typed);
    }

    private async Task<T> FinalizeSetAsync<T>(GetOrCreateState<T> state, T value, Stopwatch sw)
    {
        if (sw.IsRunning) sw.Stop();
        KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.GetOrCreate, ProviderName, sw.Elapsed);
        KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.GetOrCreate, ProviderName);
        await ObserveGetOrCreateAsync(state, KyrolusCacheObservation.Set, sw.Elapsed, null).ConfigureAwait(false);
        return value;
    }

    private static void RecordLockWait(Stopwatch waitSw)
    {
        if (waitSw.IsRunning) waitSw.Stop();
        if (waitSw.Elapsed > TimeSpan.Zero)
            KyrolusCacheInstrumentation.RecordLockWait(ProviderName, waitSw.Elapsed);
    }

    private TimeSpan GetLockRetryDelay(int attempt)
    {
        var baseDelay = options.LockRetryDelay ?? KyrolusCacheDefaults.DefaultLockRetryDelay;
        if (baseDelay <= TimeSpan.Zero) return TimeSpan.Zero;
        if (options.LockBackoffMode != KyrolusRedisLockBackoffMode.Exponential)
            return baseDelay;
        var multiplier = Math.Max(1, options.LockBackoffMultiplier);
        var factor = Math.Pow(multiplier, Math.Max(0, attempt - 1));
        var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * factor);
        if (options.LockMaxRetryDelay is { } max && max > TimeSpan.Zero && delay > max)
            return max;
        return delay;
    }

    private RedisKey ResolveKey(string cacheKey, KyrolusCacheEntryOptions? entryOptions)
    {
        var (region, tenantId) = ResolveNamespace(entryOptions);
        return keyFactory.BuildKey(cacheKey, region, tenantId);
    }

    private RedisKey ResolveTagKey(string tag, KyrolusCacheEntryOptions? entryOptions)
    {
        var (region, tenantId) = ResolveNamespace(entryOptions);
        return keyFactory.BuildTagKey(tag, region, tenantId);
    }

    private (string? Region, string? TenantId) ResolveNamespace(KyrolusCacheEntryOptions? entryOptions)
    {
        var region = entryOptions?.Region ?? options.DefaultRegion;
        var tenantId = entryOptions?.TenantId ?? options.DefaultTenantId;
        if (options.RequireRegion && string.IsNullOrWhiteSpace(region))
            throw new InvalidOperationException("Cache region is required but was not provided.");
        if (options.RequireTenantId && string.IsNullOrWhiteSpace(tenantId))
            throw new InvalidOperationException("Cache tenant id is required but was not provided.");
        return (region, tenantId);
    }

    private static RedisKey BuildEntryTagsKey(RedisKey resolvedKey) =>
        $"{resolvedKey}{EntryTagsSuffix}";

    private static RedisKey BuildSlidingKey(RedisKey resolvedKey) =>
        $"{resolvedKey}{SlidingSuffix}";

    private static RedisKey BuildLockKey(RedisKey resolvedKey) =>
        $"{resolvedKey}{LockSuffix}";

    private IEnumerable<IServer> GetServers()
    {
        foreach (var endpoint in multiplexer.GetEndPoints())
        {
            var server = multiplexer.GetServer(endpoint);
            if (!server.IsConnected) continue;
            if (options.ScanServerRole == KyrolusRedisServerRole.Any)
            {
                yield return server;
                continue;
            }
            var isReplica = server.IsReplica;
            if (options.ScanServerRole == KyrolusRedisServerRole.Primary && !isReplica)
                yield return server;
            else if (options.ScanServerRole == KyrolusRedisServerRole.Replica && isReplica)
                yield return server;
        }
    }
    private KyrolusCacheEntryOptions? ApplyPolicy<T>(KyrolusCacheEntryOptions? entryOptions, KyrolusCacheOperation operation)
    {
        var policy = policyProvider.GetPolicy(typeof(T), operation);
        if (policy is null)
            return entryOptions;
        var effective = entryOptions is null ? new KyrolusCacheEntryOptions() : CopyOptions(entryOptions);
        effective.AbsoluteExpirationRelativeToNow ??= policy.AbsoluteExpirationRelativeToNow;
        effective.SlidingExpiration ??= policy.SlidingExpiration;
        effective.Jitter ??= policy.Jitter;
        effective.NegativeExpirationRelativeToNow ??= policy.NegativeCacheTtl;
        return effective;
    }

    private static KyrolusCacheEntryOptions CopyOptions(KyrolusCacheEntryOptions entryOptions)
    {
        return new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = entryOptions.AbsoluteExpirationRelativeToNow,
            SlidingExpiration = entryOptions.SlidingExpiration,
            Jitter = entryOptions.Jitter,
            NegativeExpirationRelativeToNow = entryOptions.NegativeExpirationRelativeToNow,
            Tags = entryOptions.Tags,
            Region = entryOptions.Region,
            TenantId = entryOptions.TenantId
        };
    }

    private TimeSpan ResolveExpiration(TimeSpan? explicitExpiration, KyrolusCacheEntryOptions? options)
    {
        var ttl = explicitExpiration;
        if (ttl is null || ttl.Value == default)
        {
            ttl = options?.AbsoluteExpirationRelativeToNow
                ?? options?.SlidingExpiration
                ?? this.options.DefaultTtl
                ?? KyrolusCacheDefaults.DefaultTtl;
        }
        return ApplyJitter(ttl.Value, options?.Jitter);
    }

    private TimeSpan? ResolveNegativeTtl(KyrolusCacheEntryOptions? options)
    {
        var ttl = options?.NegativeExpirationRelativeToNow
            ?? this.options.DefaultNegativeTtl;
        if (ttl is null || ttl.Value <= TimeSpan.Zero)
            return null;
        return ApplyJitter(ttl.Value, options?.Jitter);
    }

    private static TimeSpan ApplyJitter(TimeSpan ttl, TimeSpan? jitter)
    {
        if (jitter is null || jitter.Value <= TimeSpan.Zero) return ttl;
        var max = (int)Math.Max(1, jitter.Value.TotalMilliseconds);
        var extra = TimeSpan.FromMilliseconds(Random.Shared.Next(0, max));
        return ttl + extra;
    }

    private async Task SetInternalAsync<T>(RedisKey resolvedKey, T value, TimeSpan ttl, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = serializer.Serialize(value);
        await database.StringSetAsync(resolvedKey, payload, ttl, flags: writeFlags).ConfigureAwait(false);
        await database.KeyDeleteAsync(BuildNegativeKey(resolvedKey), writeFlags).ConfigureAwait(false);
        await TrackKeyAsync(resolvedKey).ConfigureAwait(false);
        if (options?.SlidingExpiration is { } sliding)
            await database.StringSetAsync(BuildSlidingKey(resolvedKey), sliding.Ticks, ttl, flags: writeFlags).ConfigureAwait(false);
        if (options?.Tags is { Count: > 0 } tags)
            await ApplyTagsAsync([resolvedKey], tags, options, ttl).ConfigureAwait(false);
    }

    private async Task StoreOrNegativeAsync<T>(
        RedisKey resolvedKey,
        T value,
        KyrolusCacheEntryOptions? entryOptions,
        bool useNegativeCache,
        TimeSpan? negativeTtl,
        CancellationToken cancellationToken)
    {
        if (useNegativeCache && negativeTtl.HasValue && EqualityComparer<T>.Default.Equals(value!, default!))
        {
            await database.StringSetAsync(
                    BuildNegativeKey(resolvedKey),
                    (RedisValue)"1",
                    negativeTtl,
                    When.NotExists,
                    flags: writeFlags)
                .ConfigureAwait(false);
            await TrackKeyAsync(resolvedKey).ConfigureAwait(false);
            return;
        }
        var ttl = ResolveExpiration(null, entryOptions);
        await SetInternalAsync(resolvedKey, value, ttl, entryOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyTagsAsync(IEnumerable<RedisKey> keys, IReadOnlyCollection<string> tags, KyrolusCacheEntryOptions? options, TimeSpan ttl)
    {
        foreach (var tag in tags)
        {
            var tagKey = ResolveTagKey(tag, options);
            var tasks = keys.Select(key => database.SetAddAsync(tagKey, (RedisValue)key.ToString(), writeFlags)).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            await database.KeyExpireAsync(tagKey, ttl, writeFlags).ConfigureAwait(false);
        }
        foreach (var key in keys)
        {
            var entryTagsKey = BuildEntryTagsKey(key);
            var tagKeys = tags.Select(tag => (RedisValue)ResolveTagKey(tag, options).ToString()).ToArray();
            await database.SetAddAsync(entryTagsKey, tagKeys, writeFlags).ConfigureAwait(false);
            await database.KeyExpireAsync(entryTagsKey, ttl, writeFlags).ConfigureAwait(false);
        }
    }

    private async Task RemoveInternalAsync(RedisKey resolvedKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entryTagsKey = BuildEntryTagsKey(resolvedKey);
        await database.KeyDeleteAsync(BuildSlidingKey(resolvedKey), writeFlags).ConfigureAwait(false);
        await database.KeyDeleteAsync(BuildNegativeKey(resolvedKey), writeFlags).ConfigureAwait(false);
        var tagKeys = await database.SetMembersAsync(entryTagsKey, readFlags).ConfigureAwait(false);
        if (tagKeys.Length > 0)
        {
            foreach (var tagKey in tagKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await database.SetRemoveAsync(
                        (RedisKey)tagKey.ToString(),
                        (RedisValue)resolvedKey.ToString(),
                        writeFlags)
                    .ConfigureAwait(false);
            }
        }

        await database.KeyDeleteAsync(entryTagsKey, writeFlags).ConfigureAwait(false);
        await database.KeyDeleteAsync(resolvedKey, writeFlags).ConfigureAwait(false);
        await UntrackKeyAsync(resolvedKey).ConfigureAwait(false);
    }
    private async Task TrackKeyAsync(RedisKey resolvedKey)
    {
        if (patternRemovalStrategy != KyrolusRedisPatternRemovalStrategy.KeyIndex) return;
        await database.SetAddAsync(keyIndexKey, (RedisValue)resolvedKey.ToString(), writeFlags).ConfigureAwait(false);
    }
    private async Task TrackKeysAsync(IEnumerable<RedisKey> keys)
    {
        if (patternRemovalStrategy != KyrolusRedisPatternRemovalStrategy.KeyIndex) return;
        var values = keys.Select(key => (RedisValue)key.ToString()).ToArray();
        if (values.Length == 0) return;
        await database.SetAddAsync(keyIndexKey, values, writeFlags).ConfigureAwait(false);
    }
    private async Task UntrackKeyAsync(RedisKey resolvedKey)
    {
        if (patternRemovalStrategy != KyrolusRedisPatternRemovalStrategy.KeyIndex) return;
        await database.SetRemoveAsync(keyIndexKey, (RedisValue)resolvedKey.ToString(), writeFlags).ConfigureAwait(false);
    }

    private async Task RemoveByKeyIndexAsync(RedisKey resolvedPattern, CancellationToken cancellationToken)
    {
        var pattern = resolvedPattern.ToString();
        var batchSize = GetBatchSize();
        var buffer = new List<RedisKey>(batchSize);
        foreach (var entry in database.SetScan(keyIndexKey, pattern))
        {
            buffer.Add((RedisKey)entry.ToString());
            if (buffer.Count < batchSize) continue;
            await RemoveByKeysAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Clear();
        }
        if (buffer.Count > 0)
            await RemoveByKeysAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveByServerScanAsync(RedisKey resolvedPattern, CancellationToken cancellationToken)
    {
        foreach (var server in GetServers())
        {
            if (!server.Features.Scan)
                throw new InvalidOperationException("Redis server does not support SCAN; use KeyIndex removal strategy instead.");
            var keys = server.Keys(
                database: database.Database,
                pattern: (RedisValue)resolvedPattern.ToString(),
                pageSize: FallbackBatchSize);
            var batch = new List<RedisKey>(FallbackBatchSize);
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                batch.Add(key);
                if (batch.Count < FallbackBatchSize) continue;
                await RemoveByKeysAsync(batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
            if (batch.Count > 0)
                await RemoveByKeysAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RemoveByKeysAsync(IReadOnlyCollection<RedisKey> keys, CancellationToken cancellationToken)
    {
        var tasks = keys.Select(key => RemoveInternalAsync(key, cancellationToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
    private async Task RefreshSlidingAsync(RedisKey resolvedKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var slidingKey = BuildSlidingKey(resolvedKey);
        var slidingValue = await database.StringGetAsync(slidingKey, readFlags).ConfigureAwait(false);
        if (slidingValue.IsNullOrEmpty) return;

        var slidingText = slidingValue.ToString();
        if (!long.TryParse(slidingText, out var ticks)) return;

        var sliding = TimeSpan.FromTicks(ticks);
        cancellationToken.ThrowIfCancellationRequested();
        await database.KeyExpireAsync(resolvedKey, sliding, writeFlags).ConfigureAwait(false);
        await database.KeyExpireAsync(slidingKey, sliding, writeFlags).ConfigureAwait(false);
    }

    private async Task ApplyExpiryAsync(IEnumerable<RedisKey> keys, TimeSpan ttl)
    {
        var keyArray = keys as RedisKey[] ?? keys.ToArray();
        if (keyArray.Length == 0) return;
        var batchSize = GetBatchSize();
        foreach (var chunk in keyArray.Chunk(batchSize))
        {
            var batch = database.CreateBatch();
            var tasks = new Task[chunk.Length];
            for (var index = 0; index < chunk.Length; index++)
                tasks[index] = batch.KeyExpireAsync(chunk[index], ttl, writeFlags);
            batch.Execute();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
    private int GetBatchSize() => options.BatchSize > 0 ? options.BatchSize : FallbackBatchSize;
    private async Task<string?> TryAcquireLockAsync(RedisKey lockKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = Guid.NewGuid().ToString("N");
        var ttl = options.LockTtl ?? KyrolusCacheDefaults.DefaultLockTtl;
        var acquired = lockStrategy switch
        {
            KyrolusRedisLockStrategy.Lua => await TryAcquireLockLuaAsync(lockKey, token, ttl).ConfigureAwait(false),
            KyrolusRedisLockStrategy.Simple => await database.StringSetAsync(
                    lockKey,
                    token,
                    ttl,
                    When.NotExists,
                    flags: writeFlags)
                .ConfigureAwait(false),
            _ => false
        };
        return acquired ? token : null;
    }

    private Task<RedisResult> ReleaseLockAsync(RedisKey lockKey, string token)
        => database.ScriptEvaluateAsync(ReleaseLockScript, [lockKey], [(RedisValue)token], writeFlags);
    private async Task<bool> TryAcquireLockLuaAsync(RedisKey lockKey, string token, TimeSpan ttl)
    {
        var ttlMs = (long)Math.Max(1, ttl.TotalMilliseconds);
        var result = await database.ScriptEvaluateAsync(
                AcquireLockScript,
                [lockKey],
                [(RedisValue)token, ttlMs],
                writeFlags)
            .ConfigureAwait(false);
        return (int)result == 1;
    }

    private void WarnOnConfigurationChange()
    {
        if (options.WarningSink is null || string.IsNullOrWhiteSpace(options.ConfigSignatureKey))
            return;
        try
        {
            var signature = BuildConfigSignature();
            var existing = database.StringGet(configSignatureKey, readFlags);
            if (existing.IsNullOrEmpty)
            {
                database.StringSet(configSignatureKey, signature, flags: writeFlags);
                return;
            }
            if (existing.ToString() != signature)
            {
                options.WarningSink("Cache payload settings changed (compression/encryption). Clear Redis keys to avoid deserialization errors.");
                database.StringSet(configSignatureKey, signature, flags: writeFlags);
            }
        }
        catch
        {
            // Best-effort warning only.
        }
    }

    private string BuildConfigSignature()
    {
        var keyHash = HashBytes(options.EncryptionKey ?? ResolveBase64(options.EncryptionKeyBase64));
        var ivHash = HashBytes(options.EncryptionIv ?? ResolveBase64(options.EncryptionIvBase64));
        return string.Join(
            "|",
            $"cmp:{options.EnableCompression}",
            $"thr:{options.CompressionThresholdBytes}",
            $"lvl:{(int)options.CompressionLevel}",
            $"enc:{options.EnableEncryption}",
            $"key:{keyHash}",
            $"iv:{ivHash}");
    }

    private static string HashBytes(byte[]? data)
    {
        if (data is null || data.Length == 0)
            return "none";
        var hash = System.Security.Cryptography.SHA256.HashData(data);
        return Convert.ToBase64String(hash);
    }

    private static byte[]? ResolveBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;
        return Convert.FromBase64String(base64);
    }

    private static bool ShouldUseNegativeCache<T>(TimeSpan? negativeTtl)
    {
        if (negativeTtl is null || negativeTtl.Value <= TimeSpan.Zero)
            return false;
        var type = typeof(T);
        return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    }

    private static RedisKey BuildNegativeKey(RedisKey resolvedKey) =>
        $"{resolvedKey}{NegativeSuffix}";

    private bool IsGracefulFallback(Exception ex) =>
        options.EnableGracefulFallback && ex is RedisException or TimeoutException;

    private async Task HandleGracefulFallbackAsync(
        Exception ex,
        KyrolusCacheOperation operation,
        string key,
        Type? valueType,
        string? region,
        string? tenantId,
        Stopwatch sw)
    {
        if (sw.IsRunning) sw.Stop();
        if (options.CircuitBreaker.Enabled)
            circuitBreaker.ReportFailure();

        KyrolusCacheInstrumentation.RecordError(operation, ProviderName);
        await ObserveAsync(new KyrolusCacheObserverContext(
            Key: key,
            Operation: operation,
            Observation: KyrolusCacheObservation.Error,
            ValueType: valueType,
            Duration: sw.Elapsed,
            Region: region,
            TenantId: tenantId,
            Exception: ex)).ConfigureAwait(false);
    }

    private async Task<bool> EnsureConnectedAsync(
        KyrolusCacheOperation operation,
        string cacheKey,
        Type? valueType,
        string? region,
        string? tenantId)
    {
        if (options.CircuitBreaker.Enabled && !circuitBreaker.TryEnter(out var retryAfter))
        {
            var circuitException = new KyrolusRedisCircuitOpenException(retryAfter);
            if (options.CircuitBreaker.ThrowOnOpen || !options.EnableGracefulFallback)
                throw circuitException;
            await ReportUnavailableAsync(operation, cacheKey, valueType, region, tenantId, circuitException).ConfigureAwait(false);
            return false;
        }

        if (!options.EnableGracefulFallback || multiplexer.IsConnected) return true;

        circuitBreaker.ReportFailure();
        await ReportUnavailableAsync(operation, cacheKey, valueType, region, tenantId, RedisUnavailableException).ConfigureAwait(false);
        return false;
    }

    private Task ReportUnavailableAsync(
        KyrolusCacheOperation operation,
        string cacheKey,
        Type? valueType,
        string? region,
        string? tenantId,
        Exception exception)
    {
        KyrolusCacheInstrumentation.RecordError(operation, ProviderName);
        return ObserveAsync(new KyrolusCacheObserverContext(
            Key: cacheKey,
            Operation: operation,
            Observation: KyrolusCacheObservation.Error,
            ValueType: valueType,
            Duration: TimeSpan.Zero,
            Region: region,
            TenantId: tenantId,
            Exception: exception));
    }

    private static Activity? StartActivity(KyrolusCacheOperation operation, string key, string? region, string? tenantId)
    {
        var activity = KyrolusCacheInstrumentation.ActivitySource.StartActivity($"cache.{operation}");
        if (activity is null) return null;

        activity.SetTag("cache.operation", operation.ToString());
        activity.SetTag("cache.provider", ProviderName);
        activity.SetTag("cache.key", key);
        if (!string.IsNullOrWhiteSpace(region))
            activity.SetTag("cache.region", region);
        if (!string.IsNullOrWhiteSpace(tenantId))
            activity.SetTag("cache.tenant", tenantId);
        return activity;
    }

    private Task ObserveAsync(KyrolusCacheObserverContext context)
    {
        if (options.CircuitBreaker.Enabled &&
            context.Observation != KyrolusCacheObservation.Error &&
            multiplexer.IsConnected)
            circuitBreaker.ReportSuccess();
        if (observer is KyrolusNullCacheObserver) return Task.CompletedTask;
        return observer.OnObservationAsync(context);
    }

    public async Task<long> IncrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        var (region, tenantId) = ResolveNamespace(null);
        var resolvedKey = ResolveKey(cacheKey, null);
        using var activity = StartActivity(KyrolusCacheOperation.Set, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Set, cacheKey, typeof(long), region, tenantId).ConfigureAwait(false))
            return value;

        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await database.StringIncrementAsync(resolvedKey, value, flags: writeFlags).ConfigureAwait(false);
            if (expirationTime.HasValue && expirationTime.Value > TimeSpan.Zero)
            {
                await database.KeyExpireAsync(resolvedKey, expirationTime.Value, writeFlags).ConfigureAwait(false);
            }
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Set, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.Set, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Set, Observation: KyrolusCacheObservation.Set, ValueType: typeof(long), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Set, cacheKey, typeof(long), region, tenantId, sw).ConfigureAwait(false);
            return value;
        }
    }

    public async Task<long> DecrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        var (region, tenantId) = ResolveNamespace(null);
        var resolvedKey = ResolveKey(cacheKey, null);
        using var activity = StartActivity(KyrolusCacheOperation.Set, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Set, cacheKey, typeof(long), region, tenantId).ConfigureAwait(false))
            return -value;

        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await database.StringDecrementAsync(resolvedKey, value, flags: writeFlags).ConfigureAwait(false);
            if (expirationTime.HasValue && expirationTime.Value > TimeSpan.Zero)
            {
                await database.KeyExpireAsync(resolvedKey, expirationTime.Value, writeFlags).ConfigureAwait(false);
            }
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Set, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.Set, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Set, Observation: KyrolusCacheObservation.Set, ValueType: typeof(long), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Set, cacheKey, typeof(long), region, tenantId, sw).ConfigureAwait(false);
            return -value;
        }
    }

    public async Task<bool> HashSetAsync<TField>(string cacheKey, string field, TField value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        var (region, tenantId) = ResolveNamespace(null);
        var resolvedKey = ResolveKey(cacheKey, null);
        using var activity = StartActivity(KyrolusCacheOperation.Set, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Set, cacheKey, typeof(TField), region, tenantId).ConfigureAwait(false))
            return false;

        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = serializer.Serialize(value);
            var result = await database.HashSetAsync(resolvedKey, (RedisValue)field, (RedisValue)payload, flags: writeFlags).ConfigureAwait(false);
            if (expirationTime.HasValue && expirationTime.Value > TimeSpan.Zero)
            {
                await database.KeyExpireAsync(resolvedKey, expirationTime.Value, writeFlags).ConfigureAwait(false);
            }
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Set, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.Set, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Set, Observation: KyrolusCacheObservation.Set, ValueType: typeof(TField), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Set, cacheKey, typeof(TField), region, tenantId, sw).ConfigureAwait(false);
            return false;
        }
    }

    public async Task<TField?> HashGetAsync<TField>(string cacheKey, string field, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        var (region, tenantId) = ResolveNamespace(null);
        var resolvedKey = ResolveKey(cacheKey, null);
        using var activity = StartActivity(KyrolusCacheOperation.Get, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Get, cacheKey, typeof(TField), region, tenantId).ConfigureAwait(false))
            return default;

        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await database.HashGetAsync(resolvedKey, (RedisValue)field, readFlags).ConfigureAwait(false);
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Get, ProviderName, sw.Elapsed);
            if (value.IsNull)
            {
                KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.Get, ProviderName);
                await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Get, Observation: KyrolusCacheObservation.Miss, ValueType: typeof(TField), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
                return default;
            }

            KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.Get, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Get, Observation: KyrolusCacheObservation.Hit, ValueType: typeof(TField), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
            return serializer.Deserialize<TField>(value!);
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Get, cacheKey, typeof(TField), region, tenantId, sw).ConfigureAwait(false);
            return default;
        }
    }

    public async Task<IDictionary<string, TField?>> HashGetAllAsync<TField>(string cacheKey, CancellationToken cancellationToken = default)
    {
        var (region, tenantId) = ResolveNamespace(null);
        var resolvedKey = ResolveKey(cacheKey, null);
        using var activity = StartActivity(KyrolusCacheOperation.Get, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Get, cacheKey, typeof(TField), region, tenantId).ConfigureAwait(false))
            return new Dictionary<string, TField?>(StringComparer.Ordinal);

        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = await database.HashGetAllAsync(resolvedKey, readFlags).ConfigureAwait(false);
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Get, ProviderName, sw.Elapsed);

            var dict = new Dictionary<string, TField?>(entries.Length, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                dict[entry.Name!] = entry.Value.IsNull ? default : serializer.Deserialize<TField>(entry.Value!);
            }

            KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.Get, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Get, Observation: KyrolusCacheObservation.Hit, ValueType: typeof(TField), Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
            return dict;
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Get, cacheKey, typeof(TField), region, tenantId, sw).ConfigureAwait(false);
            return new Dictionary<string, TField?>(StringComparer.Ordinal);
        }
    }

    public async Task<bool> HashDeleteAsync(string cacheKey, string field, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        var (region, tenantId) = ResolveNamespace(null);
        var resolvedKey = ResolveKey(cacheKey, null);
        using var activity = StartActivity(KyrolusCacheOperation.Remove, cacheKey, region, tenantId);
        if (!await EnsureConnectedAsync(KyrolusCacheOperation.Remove, cacheKey, null, region, tenantId).ConfigureAwait(false))
            return false;

        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deleted = await database.HashDeleteAsync(resolvedKey, (RedisValue)field, writeFlags).ConfigureAwait(false);
            sw.Stop();
            KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Remove, ProviderName, sw.Elapsed);
            KyrolusCacheInstrumentation.RecordRemove(KyrolusCacheOperation.Remove, ProviderName);
            await ObserveAsync(new KyrolusCacheObserverContext(Key: cacheKey, Operation: KyrolusCacheOperation.Remove, Observation: KyrolusCacheObservation.Remove, ValueType: null, Duration: sw.Elapsed, Region: region, TenantId: tenantId, Exception: null)).ConfigureAwait(false);
            return deleted;
        }
        catch (Exception ex) when (IsGracefulFallback(ex))
        {
            await HandleGracefulFallbackAsync(ex, KyrolusCacheOperation.Remove, cacheKey, null, region, tenantId, sw).ConfigureAwait(false);
            return false;
        }
    }
}

