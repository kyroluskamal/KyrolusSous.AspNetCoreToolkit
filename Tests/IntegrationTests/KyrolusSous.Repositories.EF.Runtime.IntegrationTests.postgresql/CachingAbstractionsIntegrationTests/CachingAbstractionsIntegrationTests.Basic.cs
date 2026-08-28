using System.Diagnostics.Metrics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.CachingAbstractionsIntegrationTests;

public sealed class CachingAbstractionsIntegrationTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, byte[], byte[]?, string> AesInvalidConstructionCases => new()
    {
        { "invalid-key-size", new byte[15], null, "key" },
        { "invalid-iv-size", new byte[16], new byte[8], "iv" }
    };

    [Fact(DisplayName = "GetAllAsync uses transforming serializer cache provider and serves second call from cache")]
    public async Task GetAllAsync_WithTransformingSerializerProvider_UsesCacheRoundtrip()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var aesKey = Enumerable.Range(1, 32).Select(static i => (byte)i).ToArray();
        var aesIv = Enumerable.Range(1, 16).Select(static i => (byte)(255 - i)).ToArray();
        var serializer = new KyrolusTransformingCacheSerializer(
            new KyrolusJsonCacheSerializer(),
            [
                new KyrolusOrderedCachePayloadTransformer(new KyrolusGzipCachePayloadTransformer(minSizeBytes: 0, CompressionLevel.Optimal), order: 1),
                new KyrolusOrderedCachePayloadTransformer(new KyrolusAesCachePayloadTransformer(aesKey, aesIv), order: 2)
            ]);

        var customFactory = WithPolicy(policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IKyrolusCacheProvider>();
                services.AddSingleton(new SerializedInMemoryCacheProvider(serializer));
                services.AddSingleton<IKyrolusCacheProvider>(sp => sp.GetRequiredService<SerializedInMemoryCacheProvider>());
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var provider = scope.ServiceProvider.GetRequiredService<SerializedInMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        provider.Clear();
        provider.Count.ShouldBe(0);

        counter.Reset();
        var first = (await repo.GetAllAsync()).ToList();
        first.Count.ShouldBe(3);
        counter.Count.ShouldBeGreaterThan(0);
        provider.Count.ShouldBe(1);
        provider.LastStoredPayload.ShouldNotBeNull();
        provider.LastStoredPayload!.Length.ShouldBeGreaterThan(0);

        counter.Reset();
        var second = (await repo.GetAllAsync()).ToList();
        second.Count.ShouldBe(3);
        counter.Count.ShouldBe(0);
    }

    [Theory(DisplayName = "AES payload transformer rejects invalid construction arguments")]
    [MemberData(nameof(AesInvalidConstructionCases))]
    public void AesTransformer_InvalidConstruction_Throws(string caseId, byte[] key, byte[]? iv, string expectedParamName)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        var ex = Should.Throw<ArgumentException>(() => _ = new KyrolusAesCachePayloadTransformer(key, iv));

        ex.ParamName.ShouldBe(expectedParamName);
    }

    [Fact(DisplayName = "AES payload transformer supports dynamic IV roundtrip and validates short payload")]
    public void AesTransformer_DynamicIv_Roundtrip_AndShortPayloadValidation()
    {
        var key = Enumerable.Range(1, 16).Select(static i => (byte)i).ToArray();
        var transformer = new KyrolusAesCachePayloadTransformer(key);
        var payload = Encoding.UTF8.GetBytes("dynamic-iv-payload");

        var encrypted = transformer.Transform(payload);
        encrypted.Length.ShouldBeGreaterThan(payload.Length);

        var restored = transformer.Restore(encrypted);
        restored.ShouldBe(payload);

        Should.Throw<InvalidOperationException>(() => transformer.Restore([1, 2, 3, 4]));
    }

    [Fact(DisplayName = "GZip payload transformer restores raw, compressed, and unknown-flag payloads correctly")]
    public void GzipTransformer_RestoreVariants_Work()
    {
        var rawTransformer = new KyrolusGzipCachePayloadTransformer(minSizeBytes: 1024);
        var rawPayload = Encoding.UTF8.GetBytes("tiny");
        var raw = rawTransformer.Transform(rawPayload);
        raw.Length.ShouldBeGreaterThan(rawPayload.Length);
        rawTransformer.Restore(raw).ShouldBe(rawPayload);

        var compressedTransformer = new KyrolusGzipCachePayloadTransformer(minSizeBytes: 1, CompressionLevel.SmallestSize);
        var largePayload = Encoding.UTF8.GetBytes(new string('A', 2048));
        var compressed = compressedTransformer.Transform(largePayload);
        compressed.Length.ShouldBeGreaterThan(0);
        compressedTransformer.Restore(compressed).ShouldBe(largePayload);

        var unknownFlagPayload = new byte[] { (byte)'K', (byte)'Y', (byte)'C', (byte)'0', 9, 1, 2, 3 };
        compressedTransformer.Restore(unknownFlagPayload).ShouldBe(unknownFlagPayload);

        var noHeaderPayload = new byte[] { 7, 8, 9 };
        compressedTransformer.Restore(noHeaderPayload).ShouldBe(noHeaderPayload);
    }

    [Fact(DisplayName = "Json context cache serializer supports registered type and rejects unregistered type")]
    public void JsonContextSerializer_RegisteredAndUnregisteredTypes_WorkAsExpected()
    {
        var context = new CachingProbeJsonContext(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var serializer = new KyrolusJsonContextCacheSerializer(context);
        var payload = new CachingProbePayload("probe", 7);

        var bytes = serializer.Serialize(payload);
        var restored = serializer.Deserialize<CachingProbePayload>(bytes);

        restored.ShouldNotBeNull();
        restored!.Name.ShouldBe(payload.Name);
        restored.Count.ShouldBe(payload.Count);

        Should.Throw<InvalidOperationException>(() => serializer.Serialize(new Product { Name = "probe", Sku = "probe-sku" }));
    }

    [Fact(DisplayName = "Cache key factory composes key, tag key, and entry-tags key")]
    public void CacheKeyFactory_ComposesExpectedKeys()
    {
        var factory = new KyrolusCacheKeyFactory(" app ");

        factory.BuildKey("entity:1", region: "products", tenantId: "tenant-a").ShouldBe("app:products:tenant-a:entity:1");
        factory.BuildTagKey("featured", region: "products", tenantId: "tenant-a").ShouldBe("app:products:tenant-a:tag:featured");
        factory.BuildEntryTagsKey("entity:1").ShouldBe("app:tags:entity:1");
    }

    [Fact(DisplayName = "Cache policy registry resolves in type-operation then operation then default precedence")]
    public void CachePolicyRegistry_Precedence_Works()
    {
        var defaultPolicy = new KyrolusCachePolicy(Enabled: false, KeySuffix: "default");
        var operationPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "op");
        var typePolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "type");

        var registry = new KyrolusCachePolicyRegistry()
            .SetDefault(defaultPolicy)
            .SetForOperation(KyrolusCacheOperation.Get, operationPolicy)
            .SetForType<Product>(KyrolusCacheOperation.Get, typePolicy);

        registry.GetPolicy(typeof(Product), KyrolusCacheOperation.Get).ShouldBe(typePolicy);
        registry.GetPolicy(typeof(Category), KyrolusCacheOperation.Get).ShouldBe(operationPolicy);
        registry.GetPolicy(typeof(Category), KyrolusCacheOperation.Remove).ShouldBe(defaultPolicy);
    }

    [Fact(DisplayName = "Repository cache policy registry resolves tenant/type/operation precedence correctly")]
    public async Task RepositoryCachePolicyRegistry_Precedence_Works()
    {
        var defaultPolicy = new KyrolusCachePolicy(Enabled: false, KeySuffix: "default");
        var operationPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "operation");
        var typePolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "type");
        var tenantPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "tenant");
        var tenantOperationPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "tenant-operation");
        var tenantTypePolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "tenant-type");

        var registry = new KyrolusRepositoryCachePolicyRegistry()
            .SetDefault(defaultPolicy)
            .SetForOperation("GetAllAsync", operationPolicy)
            .SetForType<Product>("GetAllAsync", typePolicy)
            .SetForTenant("tenant-a", tenantPolicy)
            .SetForTenantOperation("tenant-a", "GetAllAsync", tenantOperationPolicy)
            .SetForTenantType<Product>("tenant-a", "GetAllAsync", tenantTypePolicy);

        var tenantTypeContext = new KyrolusRepositoryCachePolicyContext(typeof(Product), nameof(Product), "GetAllAsync", "scope", "tenant-a");
        (await registry.GetPolicyAsync(tenantTypeContext)).ShouldBe(tenantTypePolicy);

        var tenantContext = new KyrolusRepositoryCachePolicyContext(typeof(Category), nameof(Category), "GetAllAsync", "scope", "tenant-a");
        (await registry.GetPolicyAsync(tenantContext)).ShouldBe(tenantOperationPolicy);

        var operationContext = new KyrolusRepositoryCachePolicyContext(typeof(Category), nameof(Category), "GetAllAsync", "scope", "tenant-b");
        (await registry.GetPolicyAsync(operationContext)).ShouldBe(operationPolicy);

        var defaultContext = new KyrolusRepositoryCachePolicyContext(typeof(Category), nameof(Category), "UnknownOp", "scope", "tenant-b");
        (await registry.GetPolicyAsync(defaultContext)).ShouldBe(defaultPolicy);
    }

    [Fact(DisplayName = "Repository cache policy registry validates required operation and tenant arguments")]
    public void RepositoryCachePolicyRegistry_InvalidInputs_Throw()
    {
        var registry = new KyrolusRepositoryCachePolicyRegistry();
        var policy = new KyrolusCachePolicy(Enabled: true);

        Should.Throw<ArgumentException>(() => registry.SetForOperation(" ", policy));
        Should.Throw<ArgumentException>(() => registry.SetForTenant(" ", policy));
        Should.Throw<ArgumentException>(() => registry.SetForTenantOperation("tenant-a", " ", policy));
        Should.Throw<ArgumentException>(() => registry.SetForTenantOperation(" ", "GetAllAsync", policy));
        Should.Throw<ArgumentException>(() => registry.SetForTenantType<Product>("tenant-a", " ", policy));
        Should.Throw<ArgumentException>(() => registry.SetForTenantType<Product>(" ", "GetAllAsync", policy));
        Should.Throw<ArgumentException>(() => registry.SetForType<Product>(" ", policy));
        Should.Throw<ArgumentNullException>(() => registry.SetDefault(null!));
    }

    [Fact(DisplayName = "Null cache provider returns defaults and invokes factory for GetOrCreate")]
    public async Task NullCacheProvider_ReturnsDefaults_AndInvokesFactory()
    {
        var provider = KyrolusNullCacheProvider.Instance;

        await provider.SetAsync("k1", "v1");
        (await provider.GetAsync<string>("k1")).ShouldBeNull();
        (await provider.ExistsAsync("k1")).ShouldBeFalse();

        var value = await provider.GetOrCreateAsync("k2", static _ => Task.FromResult("created"));
        value.ShouldBe("created");
    }

    [Fact(DisplayName = "Cache instrumentation emits counters and histograms for cache operations")]
    public void CacheInstrumentation_EmitsMeasurements()
    {
        long hitCount = 0;
        long missCount = 0;
        long setCount = 0;
        long removeCount = 0;
        long errorCount = 0;
        long lockAcquiredCount = 0;
        long lockFailedCount = 0;
        var latencyMeasurements = 0;
        var lockWaitMeasurements = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == KyrolusCacheInstrumentation.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            switch (instrument.Name)
            {
                case "kyrolus.cache.hits": hitCount += measurement; break;
                case "kyrolus.cache.misses": missCount += measurement; break;
                case "kyrolus.cache.sets": setCount += measurement; break;
                case "kyrolus.cache.removes": removeCount += measurement; break;
                case "kyrolus.cache.errors": errorCount += measurement; break;
                case "kyrolus.cache.locks.acquired": lockAcquiredCount += measurement; break;
                case "kyrolus.cache.locks.failed": lockFailedCount += measurement; break;
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
        {
            if (instrument.Name == "kyrolus.cache.latency.ms")
                latencyMeasurements++;
            if (instrument.Name == "kyrolus.cache.lock.wait.ms")
                lockWaitMeasurements++;
        });
        listener.Start();

        KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.Get, "integration", count: 2);
        KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.Get, "integration");
        KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.Set, "integration", count: 3);
        KyrolusCacheInstrumentation.RecordRemove(KyrolusCacheOperation.Remove, "integration");
        KyrolusCacheInstrumentation.RecordError(KyrolusCacheOperation.Get, "integration");
        KyrolusCacheInstrumentation.RecordLockAcquired("integration");
        KyrolusCacheInstrumentation.RecordLockFailed("integration");
        KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Get, "integration", TimeSpan.FromMilliseconds(8));
        KyrolusCacheInstrumentation.RecordLockWait("integration", TimeSpan.FromMilliseconds(5));

        hitCount.ShouldBe(2);
        missCount.ShouldBe(1);
        setCount.ShouldBe(3);
        removeCount.ShouldBe(1);
        errorCount.ShouldBe(1);
        lockAcquiredCount.ShouldBe(1);
        lockFailedCount.ShouldBe(1);
        latencyMeasurements.ShouldBeGreaterThan(0);
        lockWaitMeasurements.ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "Cache defaults and observer primitives expose expected baseline values")]
    public async Task CacheDefaults_AndObserverPrimitives_AreUsable()
    {
        KyrolusCacheDefaults.DefaultTtl.ShouldBeGreaterThan(TimeSpan.Zero);
        KyrolusCacheDefaults.DefaultSlidingTtl.ShouldBeGreaterThan(TimeSpan.Zero);
        KyrolusCacheDefaults.DefaultLockTtl.ShouldBeGreaterThan(TimeSpan.Zero);
        KyrolusCacheDefaults.DefaultCompressionThresholdBytes.ShouldBeGreaterThan(0);

        var observer = KyrolusNullCacheObserver.Instance;
        observer.ShouldNotBeNull();
        await observer.OnObservationAsync(new KyrolusCacheObserverContext(
            Key: "test-key",
            Operation: KyrolusCacheOperation.Get,
            Observation: KyrolusCacheObservation.Hit,
            ValueType: typeof(Product),
            Duration: TimeSpan.FromMilliseconds(1),
            Region: "products",
            TenantId: "tenant-a",
            Exception: null));
        true.ShouldBeTrue();
    }

    private sealed class SerializedInMemoryCacheProvider(IKyrolusCacheSerializer serializer) : IKyrolusCacheProvider
    {
        private sealed record CacheEntry(byte[] Payload, DateTimeOffset? ExpiresAt, TimeSpan? SlidingExpiration, IReadOnlyCollection<string>? Tags);

        private readonly ConcurrentDictionary<string, CacheEntry> store = new();
        private readonly IKyrolusCacheSerializer serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        public int Count => store.Count;
        public byte[]? LastStoredPayload { get; private set; }

        public void Clear()
        {
            store.Clear();
            LastStoredPayload = null;
        }

        public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
        {
            if (!TryGetEntry(cacheKey, out var entry))
                return Task.FromResult(default(T?));

            var value = serializer.Deserialize<T>(entry.Payload);
            return Task.FromResult(value);
        }

        public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
        {
            var ttl = expirationTime > TimeSpan.Zero ? expirationTime : (TimeSpan?)null;
            SetEntry(cacheKey, value, ttl, null, null);
            return Task.CompletedTask;
        }

        public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
        {
            var ttl = ResolveTtl(value, options);
            var sliding = options?.SlidingExpiration;
            var tags = options?.Tags is null ? null : options.Tags.ToArray();
            SetEntry(cacheKey, value, ttl, sliding, tags);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            store.TryRemove(cacheKey, out _);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
            => Task.FromResult(TryGetEntry(cacheKey, out _));

        public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyPattern)) return Task.CompletedTask;

            var regex = BuildRegex(keyPattern);
            foreach (var key in store.Keys.Where(key => regex.IsMatch(key)))
                store.TryRemove(key, out _);

            return Task.CompletedTask;
        }

        public async Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, T?>(StringComparer.Ordinal);
            foreach (var key in cacheKeys)
                result[key] = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
        {
            foreach (var item in items)
                SetEntry(item.Key, item.Value, expirationTime > TimeSpan.Zero ? expirationTime : null, null, null);
            return Task.CompletedTask;
        }

        public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
        {
            foreach (var item in items)
                SetEntry(item.Key, item.Value, ResolveTtl(item.Value, options), options?.SlidingExpiration, options?.Tags?.ToArray());
            return Task.CompletedTask;
        }

        public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            foreach (var key in cacheKeys)
                store.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tag)) return Task.CompletedTask;

            foreach (var entry in store)
            {
                if (entry.Value.Tags is null) continue;
                if (entry.Value.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
                    store.TryRemove(entry.Key, out _);
            }

            return Task.CompletedTask;
        }

        public async Task<T> GetOrCreateAsync<T>(
            string cacheKey,
            Func<CancellationToken, Task<T>> factory,
            KyrolusCacheEntryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(factory);

            if (TryGetEntry(cacheKey, out var entry))
            {
                return serializer.Deserialize<T>(entry.Payload)!;
            }

            var value = await factory(cancellationToken).ConfigureAwait(false);
            if (value is null && options?.NegativeExpirationRelativeToNow is null)
            {
                return value!;
            }

            await SetAsync(cacheKey, value, options, cancellationToken).ConfigureAwait(false);
            return value!;
        }

        private bool TryGetEntry(string cacheKey, out CacheEntry entry)
        {
            entry = default!;
            if (!store.TryGetValue(cacheKey, out var existing))
                return false;

            if (IsExpired(existing))
            {
                store.TryRemove(cacheKey, out _);
                return false;
            }

            if (existing.SlidingExpiration is { } sliding && sliding > TimeSpan.Zero)
            {
                var refreshed = existing with { ExpiresAt = DateTimeOffset.UtcNow.Add(sliding) };
                store[cacheKey] = refreshed;
                entry = refreshed;
                return true;
            }

            entry = existing;
            return true;
        }

        private void SetEntry(string cacheKey, object? value, TimeSpan? ttl, TimeSpan? sliding, IReadOnlyCollection<string>? tags)
        {
            var payload = serializer.Serialize(value);
            LastStoredPayload = payload;
            var now = DateTimeOffset.UtcNow;
            var expiresAt = ResolveExpiration(now, ttl, sliding);
            store[cacheKey] = new CacheEntry(payload, expiresAt, sliding, tags);
        }

        private static bool IsExpired(CacheEntry entry)
            => entry.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow;

        private static DateTimeOffset? ResolveExpiration(DateTimeOffset now, TimeSpan? ttl, TimeSpan? sliding)
        {
            if (sliding is { } slidingValue && slidingValue > TimeSpan.Zero)
                return now.Add(slidingValue);

            if (ttl is { } ttlValue && ttlValue > TimeSpan.Zero)
                return now.Add(ttlValue);

            return null;
        }

        private static TimeSpan? ResolveTtl(object? value, KyrolusCacheEntryOptions? options)
        {
            if (options is null) return null;
            var ttl = value is null ? options.NegativeExpirationRelativeToNow : options.AbsoluteExpirationRelativeToNow;
            if (ttl is null) return null;

            if (options.Jitter is { } jitter && jitter > TimeSpan.Zero)
            {
                var extra = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * jitter.TotalMilliseconds);
                ttl = ttl.Value + extra;
            }

            return ttl;
        }

        private static Regex BuildRegex(string pattern)
        {
            var escaped = Regex.Escape(pattern);
            var regexPattern = "^" + escaped.Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return new Regex(regexPattern, RegexOptions.CultureInvariant);
        }
    }
}
