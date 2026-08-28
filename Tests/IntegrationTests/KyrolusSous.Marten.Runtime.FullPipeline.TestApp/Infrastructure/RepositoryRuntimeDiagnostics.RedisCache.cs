using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Caching.Redis;
using KyrolusSous.DataProtection.Abstractions;
using KyrolusSous.DataProtection.Redis;
using KyrolusSous.DataProtection.Runtime;
using KyrolusSous.ExceptionHandling.Abstractions;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Redis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunRedisCacheRuntimeAsync(
        string redisConnectionString,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var unique = Guid.NewGuid().ToString("N");
        var prefix = $"kyrolus:diag:cache:{unique}";
        var channel = $"kyrolus:diag:cache:{unique}:bus";
        var nearPrefix = $"{prefix}:near";
        var nearChannel = $"{channel}:near";
        const string region = "diag-cache";

        using var primaryConnection = await ConnectRedisAsync(redisConnectionString).ConfigureAwait(false);
        using var secondaryConnection = await ConnectRedisAsync(redisConnectionString).ConfigureAwait(false);

        await CleanupRedisPrefixAsync(primaryConnection, prefix, cancellationToken).ConfigureAwait(false);
        await CleanupRedisPrefixAsync(primaryConnection, nearPrefix, cancellationToken).ConfigureAwait(false);

        var logEntries = new ConcurrentQueue<string>();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new RuntimeCacheLoggerProvider(logEntries));
        });
        services.AddSingleton<IConnectionMultiplexer>(primaryConnection);
        services.AddKyrolusCacheLoggingObserver(options =>
        {
            options.LogHits = true;
            options.LogMisses = true;
            options.LogSets = true;
            options.LogRemoves = true;
            options.LogExists = true;
            options.LogErrors = true;
            options.LogLocks = true;
        });
        services.AddKyrolusRedisCacheProvider(options =>
        {
            options.KeyPrefix = prefix;
            options.DefaultRegion = region;
            options.DefaultTenantId = tenantId;
            options.DefaultNegativeTtl = TimeSpan.FromSeconds(30);
            options.RequireRegion = true;
            options.RequireTenantId = true;
            options.EnableCompression = true;
            options.CompressionThresholdBytes = 1;
            options.EnableEncryption = true;
            options.EncryptionKey = CreateDiagnosticsAesKey();
            options.EncryptionIv = CreateDiagnosticsIv();
            options.PatternRemovalStrategy = KyrolusRedisPatternRemovalStrategy.KeyIndex;
            options.CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false };
        });
        services.AddHealthChecks().AddKyrolusRedisCacheHealthChecks(
            options => options.IncludeLatency = true,
            name: "diag-redis-cache");

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IKyrolusCacheProvider>();
        var options = provider.GetRequiredService<KyrolusRedisCacheOptions>();
        var keyFactory = provider.GetRequiredService<IKyrolusCacheKeyFactory>();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();

        Require(cache is KyrolusRedisCacheProvider, "Redis cache runtime should resolve the Redis cache provider.", ref checks);
        Require(options.RequireRegion && options.RequireTenantId, "Redis cache runtime should preserve required namespace options.", ref checks);

        var entryOptions = new KyrolusCacheEntryOptions
        {
            Region = region,
            TenantId = tenantId,
            SlidingExpiration = TimeSpan.FromSeconds(30),
            NegativeExpirationRelativeToNow = TimeSpan.FromSeconds(30),
            Tags = ["tag-main", "tag-extra"]
        };

        var emptyMany = await cache.GetManyAsync<string>(Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        Require(emptyMany.Count == 0, "Redis cache runtime should return an empty dictionary for empty batched reads.", ref checks);
        await cache.SetManyAsync(Array.Empty<KeyValuePair<string, string>>(), entryOptions, cancellationToken).ConfigureAwait(false);
        await cache.RemoveManyAsync(Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        checks++;

        var extensionServices = new ServiceCollection();
        extensionServices.AddSingleton<IKyrolusCachePayloadTransformer>(new RuntimePrefixPayloadTransformer("registered|"));
        extensionServices.AddKyrolusRedisCacheProvider(redisConnectionString, providerOptions =>
        {
            providerOptions.KeyPrefix = $"{prefix}:extension";
            providerOptions.DefaultRegion = region;
            providerOptions.DefaultTenantId = tenantId;
            providerOptions.RequireRegion = true;
            providerOptions.RequireTenantId = true;
            providerOptions.EnableEncryption = true;
            providerOptions.EncryptionKeyBase64 = Convert.ToBase64String(CreateDiagnosticsAesKey());
            providerOptions.EncryptionIvBase64 = Convert.ToBase64String(CreateDiagnosticsIv());
            providerOptions.CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false };
        });
        extensionServices.AddKyrolusRedisInvalidationBus(invalidationOptions =>
        {
            invalidationOptions.Channel = $"{channel}:services";
        });
        using var extensionProvider = extensionServices.BuildServiceProvider();
        var extensionCache = extensionProvider.GetRequiredService<IKyrolusCacheProvider>();
        var extensionSerializer = extensionProvider.GetRequiredService<IKyrolusCacheSerializer>();
        Require(
            extensionCache is KyrolusRedisCacheProvider &&
            extensionSerializer is not KyrolusJsonCacheSerializer &&
            extensionProvider.GetRequiredService<IKyrolusCacheInvalidationBus>() is KyrolusRedisInvalidationBus,
            "Redis cache runtime should support connection-string registrations, serializer transformers, and invalidation-bus services.",
            ref checks);
        await extensionCache.SetAsync("extension-alpha", "payload", entryOptions, cancellationToken).ConfigureAwait(false);
        Require(
            await extensionCache.GetAsync<string>("extension-alpha", cancellationToken).ConfigureAwait(false) == "payload",
            "Redis cache runtime should round-trip entries through the connection-string registration path.",
            ref checks);

        var largePayload = new string('Z', 2048);
        await cache.SetAsync("alpha", largePayload, entryOptions, cancellationToken).ConfigureAwait(false);
        var storedPayload = await primaryConnection.GetDatabase().StringGetAsync(
            keyFactory.BuildKey("alpha", region, tenantId)).ConfigureAwait(false);
        Require(
            storedPayload.HasValue &&
            !string.Equals(storedPayload.ToString(), largePayload, StringComparison.Ordinal),
            "Redis cache runtime should store compressed and encrypted payloads.",
            ref checks);

        var alpha = await cache.GetAsync<string>("alpha", cancellationToken).ConfigureAwait(false);
        Require(alpha == largePayload, "Redis cache runtime should round-trip cache entries.", ref checks);
        Require(await cache.ExistsAsync("alpha", cancellationToken).ConfigureAwait(false), "Redis cache runtime should report existing keys.", ref checks);

        var firstMissing = await cache.GetAsync<string?>("missing", cancellationToken).ConfigureAwait(false);
        var secondMissing = await cache.GetAsync<string?>("missing", cancellationToken).ConfigureAwait(false);
        var negativeKey = keyFactory.BuildKey("missing", region, tenantId) + ":neg";
        Require(
            firstMissing is null &&
            secondMissing is null &&
            await primaryConnection.GetDatabase().KeyExistsAsync(negativeKey).ConfigureAwait(false),
            "Redis cache runtime should use negative caching for missing nullable values.",
            ref checks);

        await cache.SetManyAsync(
            [
                new KeyValuePair<string, string>("beta", "B"),
                new KeyValuePair<string, string>("gamma", "G")
            ],
            entryOptions,
            cancellationToken).ConfigureAwait(false);
        var many = await cache.GetManyAsync<string>(["beta", "gamma", "missing"], cancellationToken).ConfigureAwait(false);
        Require(
            many["beta"] == "B" &&
            many["gamma"] == "G" &&
            many["missing"] is null,
            "Redis cache runtime should support batched set/get operations.",
            ref checks);

        await cache.RemoveManyAsync(["beta", "gamma"], cancellationToken).ConfigureAwait(false);
        Require(
            !await cache.ExistsAsync("beta", cancellationToken).ConfigureAwait(false) &&
            !await cache.ExistsAsync("gamma", cancellationToken).ConfigureAwait(false),
            "Redis cache runtime should remove many entries.",
            ref checks);

        var factoryCalls = 0;
        var createdFirst = await cache.GetOrCreateAsync(
            "factory",
            _ =>
            {
                factoryCalls++;
                return Task.FromResult("factory-value");
            },
            entryOptions,
            cancellationToken).ConfigureAwait(false);
        var createdSecond = await cache.GetOrCreateAsync(
            "factory",
            _ =>
            {
                factoryCalls++;
                return Task.FromResult("other-value");
            },
            entryOptions,
            cancellationToken).ConfigureAwait(false);
        Require(
            createdFirst == "factory-value" &&
            createdSecond == "factory-value" &&
            factoryCalls == 1,
            "Redis cache runtime should lock and cache GetOrCreate results.",
            ref checks);
        await cache.SetAsync("tagged-one", "one", entryOptions, cancellationToken).ConfigureAwait(false);
        await cache.SetAsync(
            "tagged-two",
            "two",
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId,
                Tags = ["tag-main"]
            },
            cancellationToken).ConfigureAwait(false);
        await cache.RemoveByTagAsync("tag-main", cancellationToken).ConfigureAwait(false);
        Require(
            !await cache.ExistsAsync("tagged-one", cancellationToken).ConfigureAwait(false) &&
            !await cache.ExistsAsync("tagged-two", cancellationToken).ConfigureAwait(false),
            "Redis cache runtime should invalidate entries by tag.",
            ref checks);

        await cache.SetAsync("pattern-one", "1", entryOptions, cancellationToken).ConfigureAwait(false);
        await cache.SetAsync("pattern-two", "2", entryOptions, cancellationToken).ConfigureAwait(false);
        await cache.RemoveKeysByPatternAsync("pattern-*", cancellationToken).ConfigureAwait(false);
        Require(
            !await cache.ExistsAsync("pattern-one", cancellationToken).ConfigureAwait(false) &&
            !await cache.ExistsAsync("pattern-two", cancellationToken).ConfigureAwait(false),
            "Redis cache runtime should invalidate entries by pattern using the key index strategy.",
            ref checks);

        var scanOptions = new KyrolusRedisCacheOptions
        {
            KeyPrefix = $"{prefix}:scan",
            DefaultRegion = region,
            DefaultTenantId = tenantId,
            RequireRegion = true,
            RequireTenantId = true,
            PatternRemovalStrategy = KyrolusRedisPatternRemovalStrategy.ServerScan,
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false }
        };
        var scanCache = new KyrolusRedisCacheProvider(
            primaryConnection,
            new KyrolusRedisCacheDependencies(
                provider.GetRequiredService<IKyrolusCacheSerializer>(),
                new KyrolusCacheKeyFactory(scanOptions.KeyPrefix),
                scanOptions,
                provider.GetRequiredService<IKyrolusCacheObserver>(),
                provider.GetRequiredService<IKyrolusCachePolicyProvider>()));
        await scanCache.SetAsync("scan-one", "1", TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        await scanCache.SetAsync("scan-two", "2", TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        await scanCache.RemoveKeysByPatternAsync("scan-*", cancellationToken).ConfigureAwait(false);
        Require(
            !await scanCache.ExistsAsync("scan-one", cancellationToken).ConfigureAwait(false) &&
            !await scanCache.ExistsAsync("scan-two", cancellationToken).ConfigureAwait(false),
            "Redis cache runtime should invalidate entries by pattern using server scan.",
            ref checks);

        var disabledPatternOptions = CreateRedisRuntimeOptions($"{prefix}:disabled", region, tenantId);
        disabledPatternOptions.PatternRemovalStrategy = KyrolusRedisPatternRemovalStrategy.Disabled;
        var disabledPatternCache = BuildRedisCacheProvider(primaryConnection, disabledPatternOptions);
        await disabledPatternCache.SetAsync("disabled-one", "1", TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        await disabledPatternCache.RemoveKeysByPatternAsync("disabled-*", cancellationToken).ConfigureAwait(false);
        Require(
            await disabledPatternCache.ExistsAsync("disabled-one", cancellationToken).ConfigureAwait(false),
            "Redis cache runtime should leave keys untouched when pattern invalidation is disabled.",
            ref checks);

        var policyRegistry = new KyrolusCachePolicyRegistry()
            .SetForType<string>(KyrolusCacheOperation.GetOrCreate, new KyrolusCachePolicy(
                SlidingExpiration: TimeSpan.FromSeconds(20),
                NegativeCacheTtl: TimeSpan.FromSeconds(20),
                Jitter: TimeSpan.FromMilliseconds(1)));
        var policyOptions = CreateRedisRuntimeOptions($"{prefix}:policy", region, tenantId);
        var policyCache = BuildRedisCacheProvider(primaryConnection, policyOptions, policyProvider: policyRegistry);
        var policyFactoryCalls = 0;
        var policyFirst = await policyCache.GetOrCreateAsync<string?>(
            "policy-null",
            _ =>
            {
                policyFactoryCalls++;
                return Task.FromResult<string?>(null);
            },
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId
            },
            cancellationToken).ConfigureAwait(false);
        var policySecond = await policyCache.GetOrCreateAsync<string?>(
            "policy-null",
            _ =>
            {
                policyFactoryCalls++;
                return Task.FromResult<string?>("other");
            },
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId
            },
            cancellationToken).ConfigureAwait(false);
        Require(
            policyFirst is null &&
            policySecond is null &&
            policyFactoryCalls == 1,
            "Redis cache runtime should merge cache policies and reuse negative GetOrCreate hits.",
            ref checks);

        var noLockOptions = CreateRedisRuntimeOptions($"{prefix}:nolock", region, tenantId);
        noLockOptions.LockStrategy = KyrolusRedisLockStrategy.Disabled;
        var noLockCache = BuildRedisCacheProvider(primaryConnection, noLockOptions);
        var noLockCalls = 0;
        var noLockFirst = await noLockCache.GetOrCreateAsync(
            "no-lock",
            _ =>
            {
                noLockCalls++;
                return Task.FromResult("no-lock-value");
            },
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId,
                SlidingExpiration = TimeSpan.FromSeconds(20)
            },
            cancellationToken).ConfigureAwait(false);
        var noLockSecond = await noLockCache.GetOrCreateAsync(
            "no-lock",
            _ =>
            {
                noLockCalls++;
                return Task.FromResult("other");
            },
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId,
                SlidingExpiration = TimeSpan.FromSeconds(20)
            },
            cancellationToken).ConfigureAwait(false);
        Require(
            noLockFirst == "no-lock-value" &&
            noLockSecond == "no-lock-value" &&
            noLockCalls == 1,
            "Redis cache runtime should support lock-free GetOrCreate paths.",
            ref checks);

        var simpleLockOptions = CreateRedisRuntimeOptions($"{prefix}:simple", region, tenantId);
        simpleLockOptions.LockStrategy = KyrolusRedisLockStrategy.Simple;
        var simpleLockCache = BuildRedisCacheProvider(primaryConnection, simpleLockOptions);
        Require(
            await simpleLockCache.GetOrCreateAsync(
                "simple-lock",
                _ => Task.FromResult("simple-lock-value"),
                new KyrolusCacheEntryOptions
                {
                    Region = region,
                    TenantId = tenantId
                },
                cancellationToken).ConfigureAwait(false) == "simple-lock-value",
            "Redis cache runtime should support the simple lock strategy for GetOrCreate.",
            ref checks);

        var retryOptions = CreateRedisRuntimeOptions($"{prefix}:retry", region, tenantId);
        retryOptions.LockStrategy = KyrolusRedisLockStrategy.Simple;
        retryOptions.LockWait = TimeSpan.FromMilliseconds(60);
        retryOptions.LockRetryDelay = TimeSpan.FromMilliseconds(5);
        retryOptions.LockBackoffMode = KyrolusRedisLockBackoffMode.Exponential;
        retryOptions.LockBackoffMultiplier = 2;
        retryOptions.LockMaxRetryDelay = TimeSpan.FromMilliseconds(10);
        var retryFactory = new KyrolusCacheKeyFactory(retryOptions.KeyPrefix);
        var retryResolvedKey = retryFactory.BuildKey("contended", region, tenantId);
        var retryLockKey = $"{retryResolvedKey}:lock";
        await primaryConnection.GetDatabase().StringSetAsync(
            retryLockKey,
            "held",
            retryOptions.LockWait!.Value + TimeSpan.FromSeconds(1),
            flags: CommandFlags.None).ConfigureAwait(false);
        var retryCache = BuildRedisCacheProvider(primaryConnection, retryOptions);
        var retryCalls = 0;
        Require(
            await retryCache.GetOrCreateAsync(
                "contended",
                _ =>
                {
                    retryCalls++;
                    return Task.FromResult("retry-value");
                },
                new KyrolusCacheEntryOptions
                {
                    Region = region,
                    TenantId = tenantId
                },
                cancellationToken).ConfigureAwait(false) == "retry-value" &&
            retryCalls == 1,
            "Redis cache runtime should fall back after contended locks with exponential backoff.",
            ref checks);

        var cachedAfterLockOptions = CreateRedisRuntimeOptions($"{prefix}:cached-after-lock", region, tenantId);
        cachedAfterLockOptions.LockStrategy = KyrolusRedisLockStrategy.Simple;
        cachedAfterLockOptions.LockWait = TimeSpan.FromMilliseconds(50);
        cachedAfterLockOptions.LockRetryDelay = TimeSpan.FromMilliseconds(2);
        cachedAfterLockOptions.LockBackoffMode = KyrolusRedisLockBackoffMode.Fixed;
        var cachedAfterLockCache = BuildRedisCacheProvider(primaryConnection, cachedAfterLockOptions);
        var cachedAfterLockFactory = new KyrolusCacheKeyFactory(cachedAfterLockOptions.KeyPrefix);
        var cachedAfterLockResolvedKey = cachedAfterLockFactory.BuildKey("cached-after-lock", region, tenantId);
        var cachedAfterLockEntryOptions = new KyrolusCacheEntryOptions
        {
            Region = region,
            TenantId = tenantId,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        };
        await cachedAfterLockCache.SetAsync(
            "cached-after-lock",
            "cached-value",
            cachedAfterLockEntryOptions,
            cancellationToken).ConfigureAwait(false);
        await primaryConnection.GetDatabase().StringSetAsync(
            $"{cachedAfterLockResolvedKey}:lock",
            "held",
            cachedAfterLockOptions.LockWait!.Value + TimeSpan.FromSeconds(1),
            flags: CommandFlags.None).ConfigureAwait(false);
        var cachedAfterLockFactoryCalls = 0;
        Require(
            await cachedAfterLockCache.GetOrCreateAsync(
                "cached-after-lock",
                _ =>
                {
                    cachedAfterLockFactoryCalls++;
                    return Task.FromResult("factory-value");
                },
                cachedAfterLockEntryOptions,
                cancellationToken).ConfigureAwait(false) == "cached-value" &&
            cachedAfterLockFactoryCalls == 0,
            "Redis cache runtime should return an existing cached value after lock contention without invoking the factory.",
            ref checks);

        var keyIndexBatchOptions = CreateRedisRuntimeOptions($"{prefix}:keyindex-batch", region, tenantId);
        keyIndexBatchOptions.BatchSize = 1;
        var keyIndexBatchCache = BuildRedisCacheProvider(primaryConnection, keyIndexBatchOptions);
        await keyIndexBatchCache.SetManyAsync(
            [
                new KeyValuePair<string, string>("batch-key-1", "1"),
                new KeyValuePair<string, string>("batch-key-2", "2"),
                new KeyValuePair<string, string>("batch-key-3", "3")
            ],
            cachedAfterLockEntryOptions,
            cancellationToken).ConfigureAwait(false);
        await keyIndexBatchCache.RemoveKeysByPatternAsync("batch-key-*", cancellationToken).ConfigureAwait(false);
        Require(
            !await keyIndexBatchCache.ExistsAsync("batch-key-1", cancellationToken).ConfigureAwait(false) &&
            !await keyIndexBatchCache.ExistsAsync("batch-key-2", cancellationToken).ConfigureAwait(false) &&
            !await keyIndexBatchCache.ExistsAsync("batch-key-3", cancellationToken).ConfigureAwait(false),
            "Redis cache runtime should flush key-index invalidation batches when the configured batch size is reached.",
            ref checks);

        var scanBatchOptions = CreateRedisRuntimeOptions($"{prefix}:scan-batch", region, tenantId);
        scanBatchOptions.PatternRemovalStrategy = KyrolusRedisPatternRemovalStrategy.ServerScan;
        scanBatchOptions.ScanServerRole = KyrolusRedisServerRole.Any;
        var scanBatchCache = BuildRedisCacheProvider(primaryConnection, scanBatchOptions);
        var scanBatchEntries = Enumerable.Range(0, 257)
            .Select(index => new KeyValuePair<string, string>(
                $"scan-batch-{index:D3}",
                index.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        await scanBatchCache.SetManyAsync(scanBatchEntries, cachedAfterLockEntryOptions, cancellationToken).ConfigureAwait(false);
        await scanBatchCache.RemoveKeysByPatternAsync("scan-batch-*", cancellationToken).ConfigureAwait(false);
        Require(
            !await scanBatchCache.ExistsAsync("scan-batch-000", cancellationToken).ConfigureAwait(false) &&
            !await scanBatchCache.ExistsAsync("scan-batch-128", cancellationToken).ConfigureAwait(false) &&
            !await scanBatchCache.ExistsAsync("scan-batch-256", cancellationToken).ConfigureAwait(false),
            "Redis cache runtime should flush server-scan invalidation batches and support scanning across any connected server role.",
            ref checks);

        var missingRegionCache = BuildRedisCacheProvider(
            primaryConnection,
            CreateRedisRuntimeOptions($"{prefix}:missing-region", region, tenantId));
        await ExpectThrowsAsync<InvalidOperationException>(
            () => missingRegionCache.SetAsync(
                "missing-region",
                "value",
                new KyrolusCacheEntryOptions
                {
                    Region = " ",
                    TenantId = tenantId
                },
                cancellationToken)).ConfigureAwait(false);
        checks++;

        var missingTenantCache = BuildRedisCacheProvider(
            primaryConnection,
            CreateRedisRuntimeOptions($"{prefix}:missing-tenant", region, tenantId));
        await ExpectThrowsAsync<InvalidOperationException>(
            () => missingTenantCache.SetAsync(
                "missing-tenant",
                "value",
                new KyrolusCacheEntryOptions
                {
                    Region = region,
                    TenantId = " "
                },
                cancellationToken)).ConfigureAwait(false);
        checks++;

        var base64SignatureProvider = BuildRedisCacheProvider(primaryConnection, new KyrolusRedisCacheOptions
        {
            KeyPrefix = $"{prefix}:base64-signature",
            DefaultRegion = region,
            DefaultTenantId = tenantId,
            RequireRegion = true,
            RequireTenantId = true,
            EnableEncryption = true,
            EncryptionKeyBase64 = Convert.ToBase64String(CreateDiagnosticsAesKey()),
            EncryptionIvBase64 = Convert.ToBase64String(CreateDiagnosticsIv()),
            WarningSink = _ => { },
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false }
        });
        Require(
            base64SignatureProvider is KyrolusRedisCacheProvider,
            "Redis cache runtime should build payload signatures from valid base64 encryption settings.",
            ref checks);

        _ = BuildRedisCacheProvider(primaryConnection, new KyrolusRedisCacheOptions
        {
            KeyPrefix = $"{prefix}:warning-catch",
            DefaultRegion = region,
            DefaultTenantId = tenantId,
            RequireRegion = true,
            RequireTenantId = true,
            WarningSink = _ => { },
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false }
        });
        var warningCatchProvider = BuildRedisCacheProvider(primaryConnection, new KyrolusRedisCacheOptions
        {
            KeyPrefix = $"{prefix}:warning-catch",
            DefaultRegion = region,
            DefaultTenantId = tenantId,
            RequireRegion = true,
            RequireTenantId = true,
            EnableCompression = true,
            CompressionThresholdBytes = 1,
            WarningSink = _ => throw new InvalidOperationException("Simulated warning sink failure."),
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false }
        });
        Require(
            warningCatchProvider is KyrolusRedisCacheProvider,
            "Redis cache runtime should swallow configuration-signature failures because warnings are best-effort only.",
            ref checks);

        var gracefulCircuitObserver = new RedisRuntimeObserver();
        var gracefulCircuitOptions = CreateRedisRuntimeOptions($"{prefix}:faults-circuit", region, tenantId);
        gracefulCircuitOptions.EnableGracefulFallback = true;
        gracefulCircuitOptions.CircuitBreaker = new KyrolusRedisCircuitBreakerOptions
        {
            Enabled = true,
            FailureThreshold = 20,
            OpenDuration = TimeSpan.FromSeconds(1)
        };
        var gracefulCircuitSeedCache = BuildRedisCacheProvider(
            primaryConnection,
            gracefulCircuitOptions,
            observer: gracefulCircuitObserver);
        await gracefulCircuitSeedCache.SetAsync(
            "deserialize-fault-circuit",
            "value",
            cachedAfterLockEntryOptions,
            cancellationToken).ConfigureAwait(false);
        var gracefulCircuitCache = BuildRedisCacheProvider(
            primaryConnection,
            gracefulCircuitOptions,
            observer: gracefulCircuitObserver,
            serializer: new ThrowingRedisCacheSerializer(throwOnDeserialize: true));
        Require(
            await gracefulCircuitCache.GetAsync<string>("deserialize-fault-circuit", cancellationToken).ConfigureAwait(false) is null &&
            gracefulCircuitObserver.ErrorObservations.Any(context =>
                context.Operation == KyrolusCacheOperation.Get &&
                string.Equals(context.Key, "deserialize-fault-circuit", StringComparison.Ordinal)),
            "Redis cache runtime should report graceful-fallback failures through the circuit-breaker-enabled path.",
            ref checks);

        var activitySnapshots = new ConcurrentQueue<Dictionary<string, string?>>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, KyrolusCacheInstrumentation.ActivitySourceName, StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                var snapshot = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var tag in activity.TagObjects)
                {
                    snapshot[tag.Key] = tag.Value?.ToString();
                }

                activitySnapshots.Enqueue(snapshot);
            }
        };
        ActivitySource.AddActivityListener(activityListener);
        var activityOptions = CreateRedisRuntimeOptions($"{prefix}:activity", region, tenantId);
        activityOptions.CircuitBreaker = new KyrolusRedisCircuitBreakerOptions
        {
            Enabled = true,
            FailureThreshold = 10,
            OpenDuration = TimeSpan.FromSeconds(1)
        };
        var activityCache = BuildRedisCacheProvider(primaryConnection, activityOptions);
        await activityCache.SetAsync(
            "activity-key",
            "activity-value",
            cachedAfterLockEntryOptions,
            cancellationToken).ConfigureAwait(false);
        Require(
            await activityCache.GetAsync<string>("activity-key", cancellationToken).ConfigureAwait(false) == "activity-value" &&
            activitySnapshots.Any(snapshot =>
                snapshot.TryGetValue("cache.operation", out var operation) &&
                string.Equals(operation, nameof(KyrolusCacheOperation.Set), StringComparison.Ordinal) &&
                snapshot.TryGetValue("cache.provider", out var providerName) &&
                string.Equals(providerName, "redis", StringComparison.Ordinal) &&
                snapshot.TryGetValue("cache.region", out var activityRegion) &&
                string.Equals(activityRegion, region, StringComparison.Ordinal) &&
                snapshot.TryGetValue("cache.tenant", out var activityTenant) &&
                string.Equals(activityTenant, tenantId, StringComparison.Ordinal)) &&
            activitySnapshots.Any(snapshot =>
                snapshot.TryGetValue("cache.operation", out var operation) &&
                string.Equals(operation, nameof(KyrolusCacheOperation.Get), StringComparison.Ordinal)),
            "Redis cache runtime should emit activity tags for cache operations when telemetry listeners are attached.",
            ref checks);

        var warningMessages = new ConcurrentQueue<string>();
        var warningPrefix = $"{prefix}:warning";
        _ = BuildRedisCacheProvider(primaryConnection, new KyrolusRedisCacheOptions
        {
            KeyPrefix = warningPrefix,
            DefaultRegion = region,
            DefaultTenantId = tenantId,
            RequireRegion = true,
            RequireTenantId = true,
            WarningSink = warningMessages.Enqueue,
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false }
        });
        _ = BuildRedisCacheProvider(primaryConnection, new KyrolusRedisCacheOptions
        {
            KeyPrefix = warningPrefix,
            DefaultRegion = region,
            DefaultTenantId = tenantId,
            RequireRegion = true,
            RequireTenantId = true,
            EnableCompression = true,
            CompressionThresholdBytes = 1,
            WarningSink = warningMessages.Enqueue,
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false }
        });
        Require(
            warningMessages.Any(message => message.Contains("Cache payload settings changed", StringComparison.Ordinal)),
            "Redis cache runtime should warn when payload settings change for an existing keyspace.",
            ref checks);

        var disabledCircuitBreaker = CreateRedisCircuitBreaker(new KyrolusRedisCircuitBreakerOptions { Enabled = false });
        Require(
            TryEnterRedisCircuitBreaker(disabledCircuitBreaker, out var disabledRetryAfter) &&
            disabledRetryAfter is null,
            "Redis cache runtime should allow immediate access when the circuit breaker is disabled.",
            ref checks);
        ReportRedisCircuitBreakerSuccess(disabledCircuitBreaker);
        ReportRedisCircuitBreakerFailure(disabledCircuitBreaker);
        checks++;

        var openCircuitBreaker = CreateRedisCircuitBreaker(new KyrolusRedisCircuitBreakerOptions
        {
            Enabled = true,
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromMilliseconds(40),
            HalfOpenSuccesses = 2,
            MaxOpenDuration = TimeSpan.FromMilliseconds(100)
        });
        Require(TryEnterRedisCircuitBreaker(openCircuitBreaker, out _) , "Redis cache runtime should allow entry before the circuit opens.", ref checks);
        ReportRedisCircuitBreakerFailure(openCircuitBreaker);
        Require(TryEnterRedisCircuitBreaker(openCircuitBreaker, out _), "Redis cache runtime should stay closed before the failure threshold is reached.", ref checks);
        ReportRedisCircuitBreakerFailure(openCircuitBreaker);
        Require(
            !TryEnterRedisCircuitBreaker(openCircuitBreaker, out var openRetryAfter) &&
            openRetryAfter > TimeSpan.Zero,
            "Redis cache runtime should reject entry while the circuit is open.",
            ref checks);
        await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        Require(TryEnterRedisCircuitBreaker(openCircuitBreaker, out _), "Redis cache runtime should re-enter after the open window elapses.", ref checks);
        ReportRedisCircuitBreakerSuccess(openCircuitBreaker);
        Require(
            TryEnterRedisCircuitBreaker(openCircuitBreaker, out _),
            "Redis cache runtime should allow half-open probes after the open window elapses.",
            ref checks);
        ReportRedisCircuitBreakerSuccess(openCircuitBreaker);
        Require(TryEnterRedisCircuitBreaker(openCircuitBreaker, out _), "Redis cache runtime should close again after enough half-open successes.", ref checks);

        var clampedCircuitBreaker = CreateRedisCircuitBreaker(new KyrolusRedisCircuitBreakerOptions
        {
            Enabled = true,
            FailureThreshold = 1,
            OpenDuration = TimeSpan.Zero,
            MaxOpenDuration = TimeSpan.FromMilliseconds(15),
            BackoffMultiplier = 0.5
        });
        ReportRedisCircuitBreakerFailure(clampedCircuitBreaker);
        Require(
            !TryEnterRedisCircuitBreaker(clampedCircuitBreaker, out var clampedRetryAfter) &&
            clampedRetryAfter <= TimeSpan.FromMilliseconds(15),
            "Redis cache runtime should clamp circuit-breaker open duration when max duration is configured.",
            ref checks);
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        Require(TryEnterRedisCircuitBreaker(clampedCircuitBreaker, out _), "Redis cache runtime should recover after the clamped open duration.", ref checks);

        var healthReport = await healthChecks.CheckHealthAsync(
            registration => string.Equals(registration.Name, "diag-redis-cache", StringComparison.Ordinal),
            cancellationToken).ConfigureAwait(false);
        Require(
            healthReport.Entries.TryGetValue("diag-redis-cache", out var healthEntry) &&
            healthEntry.Status == HealthStatus.Healthy &&
            healthEntry.Data.ContainsKey("latency_ms"),
            "Redis cache health checks should report healthy status with latency metadata.",
            ref checks);

        var busOptions = new KyrolusRedisInvalidationOptions { Channel = channel };
        var receivedMessage = new TaskCompletionSource<KyrolusCacheInvalidationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriberBus = new KyrolusRedisInvalidationBus(secondaryConnection, busOptions);
        using (subscriberBus.Subscribe(message =>
        {
            receivedMessage.TrySetResult(message);
            return Task.CompletedTask;
        }))
        {
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            var publisherBus = new KyrolusRedisInvalidationBus(primaryConnection, busOptions);
            await publisherBus.PublishAsync(
                new KyrolusCacheInvalidationMessage(KyrolusCacheInvalidationKind.Keys, ["one", "two"]),
                cancellationToken).ConfigureAwait(false);
            var decodedMessage = await receivedMessage.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            Require(
                decodedMessage.Kind == KyrolusCacheInvalidationKind.Keys &&
                decodedMessage.Values.SequenceEqual(["one", "two"]),
                "Redis invalidation bus should publish and decode messages.",
                ref checks);
        }

        var noopBus = new KyrolusRedisInvalidationBus(primaryConnection, new KyrolusRedisInvalidationOptions
        {
            Channel = $"{channel}:noop",
            Publish = false,
            Subscribe = false
        });
        using (noopBus.Subscribe(_ => Task.CompletedTask))
        {
            await noopBus.PublishAsync(
                new KyrolusCacheInvalidationMessage(KyrolusCacheInvalidationKind.Key, ["ignored"]),
                cancellationToken).ConfigureAwait(false);
            checks++;
        }

        using var nearProviderA = BuildNearCacheServiceProvider(primaryConnection, nearPrefix, tenantId, nearChannel);
        using var nearProviderB = BuildNearCacheServiceProvider(secondaryConnection, nearPrefix, tenantId, nearChannel);
        var nearCacheA = nearProviderA.GetRequiredService<IKyrolusCacheProvider>();
        var nearCacheB = nearProviderB.GetRequiredService<IKyrolusCacheProvider>();
        Require(
            nearCacheA is KyrolusRedisNearCacheProvider &&
            nearCacheB is KyrolusRedisNearCacheProvider,
            "Redis near cache runtime should resolve the near cache provider.",
            ref checks);

        await nearCacheA.SetAsync("shared", "near", TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        var nearShared = await nearCacheB.GetAsync<string>("shared", cancellationToken).ConfigureAwait(false);
        Require(nearShared == "near", "Redis near cache runtime should populate L1 from Redis.", ref checks);
        Require(await nearCacheA.ExistsAsync("shared", cancellationToken).ConfigureAwait(false), "Redis near cache runtime should delegate existence checks to Redis.", ref checks);

        await nearCacheA.RemoveAsync("shared", cancellationToken).ConfigureAwait(false);
        var nearInvalidated = await AwaitConditionAsync(
            async () => await nearCacheB.GetAsync<string>("shared", cancellationToken).ConfigureAwait(false) is null,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
        Require(nearInvalidated, "Redis near cache runtime should invalidate peer L1 caches after remove.", ref checks);

        await nearCacheA.SetManyAsync(
            [
                new KeyValuePair<string, string>("bulk-a", "1"),
                new KeyValuePair<string, string>("bulk-b", "2")
            ],
            TimeSpan.FromMinutes(1),
            cancellationToken).ConfigureAwait(false);
        var nearMany = await nearCacheB.GetManyAsync<string>(["bulk-a", "bulk-b"], cancellationToken).ConfigureAwait(false);
        Require(
            nearMany["bulk-a"] == "1" && nearMany["bulk-b"] == "2",
            "Redis near cache runtime should support bulk reads.",
            ref checks);
        var nearManyCached = await nearCacheB.GetManyAsync<string>(["bulk-a", "bulk-b"], cancellationToken).ConfigureAwait(false);
        Require(
            nearManyCached["bulk-a"] == "1" && nearManyCached["bulk-b"] == "2",
            "Redis near cache runtime should serve repeated bulk reads from L1 without Redis misses.",
            ref checks);

        await nearCacheA.RemoveKeysByPatternAsync("bulk-*", cancellationToken).ConfigureAwait(false);
        var nearPatternInvalidated = await AwaitConditionAsync(
            async () =>
                await nearCacheB.GetAsync<string>("bulk-a", cancellationToken).ConfigureAwait(false) is null &&
                await nearCacheB.GetAsync<string>("bulk-b", cancellationToken).ConfigureAwait(false) is null,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
        Require(nearPatternInvalidated, "Redis near cache runtime should invalidate peer caches by pattern.", ref checks);

        var nearEntryOptions = new KyrolusCacheEntryOptions
        {
            Region = "diag-near",
            TenantId = tenantId,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
            Tags = ["near-tag"]
        };
        await nearCacheA.SetAsync("tagged", "value", nearEntryOptions, cancellationToken).ConfigureAwait(false);
        Require(
            await nearCacheB.GetAsync<string>("tagged", cancellationToken).ConfigureAwait(false) == "value",
            "Redis near cache runtime should honor the options-based set overload.",
            ref checks);
        await nearCacheA.RemoveByTagAsync("near-tag", cancellationToken).ConfigureAwait(false);
        var nearTagInvalidated = await AwaitConditionAsync(
            async () => await nearCacheA.GetAsync<string>("tagged", cancellationToken).ConfigureAwait(false) is null,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
        Require(nearTagInvalidated, "Redis near cache runtime should invalidate local L1 entries by tag.", ref checks);

        await nearCacheA.SetManyAsync(
            [
                new KeyValuePair<string, string>("group-a", "10"),
                new KeyValuePair<string, string>("group-b", "20")
            ],
            new KyrolusCacheEntryOptions
            {
                Region = "diag-near",
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
                Tags = ["near-group"]
            },
            cancellationToken).ConfigureAwait(false);
        var grouped = await nearCacheB.GetManyAsync<string>(["group-a", "group-b"], cancellationToken).ConfigureAwait(false);
        Require(
            grouped["group-a"] == "10" && grouped["group-b"] == "20",
            "Redis near cache runtime should honor the options-based bulk set overload.",
            ref checks);
        await nearCacheA.RemoveManyAsync(["group-a", "group-b"], cancellationToken).ConfigureAwait(false);
        var nearManyInvalidated = await AwaitConditionAsync(
            async () =>
                await nearCacheB.GetAsync<string>("group-a", cancellationToken).ConfigureAwait(false) is null &&
                await nearCacheB.GetAsync<string>("group-b", cancellationToken).ConfigureAwait(false) is null,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
        Require(nearManyInvalidated, "Redis near cache runtime should invalidate peer caches after bulk removes.", ref checks);

        await nearCacheA.SetAsync<string?>(
            "nullable",
            null,
            new KyrolusCacheEntryOptions
            {
                Region = "diag-near",
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken).ConfigureAwait(false);
        var nullableMany = await nearCacheB.GetManyAsync<string?>(["nullable"], cancellationToken).ConfigureAwait(false);
        var nullableCached = await nearCacheB.GetAsync<string?>("nullable", cancellationToken).ConfigureAwait(false);
        Require(
            nullableMany["nullable"] is null &&
            nullableCached is null,
            "Redis near cache runtime should cache existing null payloads in L1.",
            ref checks);

        var nearFactoryCalls = 0;
        var nearFactoryFirst = await nearCacheA.GetOrCreateAsync(
            "near-factory",
            _ =>
            {
                nearFactoryCalls++;
                return Task.FromResult("factory-near");
            },
            new KyrolusCacheEntryOptions
            {
                Region = "diag-near",
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken).ConfigureAwait(false);
        var nearFactorySecond = await nearCacheA.GetOrCreateAsync(
            "near-factory",
            _ =>
            {
                nearFactoryCalls++;
                return Task.FromResult("other");
            },
            new KyrolusCacheEntryOptions
            {
                Region = "diag-near",
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken).ConfigureAwait(false);
        Require(
            nearFactoryFirst == "factory-near" &&
            nearFactorySecond == "factory-near" &&
            nearFactoryCalls == 1,
            "Redis near cache runtime should satisfy repeated GetOrCreate requests from L1.",
            ref checks);

        var emptyNearMany = await nearCacheA.GetManyAsync<string>(Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        Require(emptyNearMany.Count == 0, "Redis near cache runtime should return an empty dictionary for empty batched reads.", ref checks);

        using var noPublishNearProvider = BuildNearCacheServiceProvider(
            primaryConnection,
            $"{nearPrefix}:nopublish",
            tenantId,
            $"{nearChannel}:nopublish",
            configureNearCache: nearOptions =>
            {
                nearOptions.PublishInvalidations = false;
                nearOptions.SubscribeInvalidations = false;
            });
        var noPublishNearCache = noPublishNearProvider.GetRequiredService<IKyrolusCacheProvider>();
        await noPublishNearCache.SetAsync("nopublish", "value", TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        await noPublishNearCache.SetManyAsync(
            [new KeyValuePair<string, string>("nopublish-bulk", "value")],
            TimeSpan.FromMinutes(1),
            cancellationToken).ConfigureAwait(false);
        await noPublishNearCache.RemoveAsync("nopublish", cancellationToken).ConfigureAwait(false);
        await noPublishNearCache.RemoveManyAsync(["nopublish-bulk"], cancellationToken).ConfigureAwait(false);
        await noPublishNearCache.RemoveKeysByPatternAsync("nopublish*", cancellationToken).ConfigureAwait(false);
        checks++;

        var nearConnectionStringServices = new ServiceCollection();
        nearConnectionStringServices.AddKyrolusRedisNearCache(
            redisConnectionString,
            configure: cacheOptions =>
            {
                cacheOptions.KeyPrefix = $"{nearPrefix}:connection";
                cacheOptions.DefaultRegion = "diag-near";
                cacheOptions.DefaultTenantId = tenantId;
                cacheOptions.RequireRegion = true;
                cacheOptions.RequireTenantId = true;
                cacheOptions.CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false };
            },
            configureNearCache: nearOptions =>
            {
                nearOptions.InvalidationChannel = $"{nearChannel}:connection";
                nearOptions.PublishInvalidations = false;
                nearOptions.SubscribeInvalidations = false;
            });
        using var nearConnectionStringProvider = nearConnectionStringServices.BuildServiceProvider();
        Require(
            nearConnectionStringProvider.GetRequiredService<IKyrolusCacheProvider>() is KyrolusRedisNearCacheProvider,
            "Redis near cache runtime should support the connection-string registration overload.",
            ref checks);

        var directNearCacheOptions = CreateRedisRuntimeOptions($"{nearPrefix}:direct", "diag-near", tenantId);
        using var nullObserverMemory = new MemoryCache(new MemoryCacheOptions());
        using var nullObserverNearCache = new KyrolusRedisNearCacheProvider(
            nullObserverMemory,
            primaryConnection,
            new KyrolusRedisCacheDependencies(
                new KyrolusJsonCacheSerializer(),
                new KyrolusCacheKeyFactory(directNearCacheOptions.KeyPrefix),
                directNearCacheOptions,
                KyrolusNullCacheObserver.Instance,
                KyrolusNullCachePolicyProvider.Instance),
            new KyrolusRedisNearCacheOptions
            {
                InvalidationChannel = $"{nearChannel}:direct",
                PublishInvalidations = false,
                SubscribeInvalidations = false,
                DefaultL1Ttl = null,
                DefaultL1SlidingTtl = null,
                L1Jitter = TimeSpan.FromMilliseconds(5)
            },
            invalidationBus: null);
        await nullObserverNearCache.SetAsync("typed-value", 5, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        var typedAsLong = await nullObserverNearCache.GetAsync<long>("typed-value", cancellationToken).ConfigureAwait(false);
        Require(
            typedAsLong == 5L,
            "Redis near cache runtime should repopulate L1 when the cached CLR type does not match the requested generic type.",
            ref checks);

        using var nullValueWriterMemory = new MemoryCache(new MemoryCacheOptions());
        using var nullValueWriter = new KyrolusRedisNearCacheProvider(
            nullValueWriterMemory,
            primaryConnection,
            new KyrolusRedisCacheDependencies(
                new KyrolusJsonCacheSerializer(),
                new KyrolusCacheKeyFactory($"{nearPrefix}:nullable"),
                CreateRedisRuntimeOptions($"{nearPrefix}:nullable", "diag-near", tenantId),
                KyrolusNullCacheObserver.Instance,
                KyrolusNullCachePolicyProvider.Instance),
            new KyrolusRedisNearCacheOptions
            {
                InvalidationChannel = $"{nearChannel}:nullable-writer",
                PublishInvalidations = false,
                SubscribeInvalidations = false,
                DefaultL1Ttl = TimeSpan.FromMinutes(1)
            },
            invalidationBus: null);
        using var nullValueReaderMemory = new MemoryCache(new MemoryCacheOptions());
        using var nullValueReader = new KyrolusRedisNearCacheProvider(
            nullValueReaderMemory,
            primaryConnection,
            new KyrolusRedisCacheDependencies(
                new KyrolusJsonCacheSerializer(),
                new KyrolusCacheKeyFactory($"{nearPrefix}:nullable"),
                CreateRedisRuntimeOptions($"{nearPrefix}:nullable", "diag-near", tenantId),
                KyrolusNullCacheObserver.Instance,
                KyrolusNullCachePolicyProvider.Instance),
            new KyrolusRedisNearCacheOptions
            {
                InvalidationChannel = $"{nearChannel}:nullable-reader",
                PublishInvalidations = false,
                SubscribeInvalidations = false,
                DefaultL1Ttl = TimeSpan.FromMinutes(1)
            },
            invalidationBus: null);
        await nullValueWriter.SetAsync<string?>(
            "nullable-direct",
            null,
            new KyrolusCacheEntryOptions
            {
                Region = "diag-near",
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken).ConfigureAwait(false);
        var firstNullLoad = await nullValueReader.GetAsync<string?>("nullable-direct", cancellationToken).ConfigureAwait(false);
        var secondNullLoad = await nullValueReader.GetAsync<string?>("nullable-direct", cancellationToken).ConfigureAwait(false);
        Require(
            firstNullLoad is null && secondNullLoad is null,
            "Redis near cache runtime should cache null payloads after fetching them from Redis.",
            ref checks);
        await nullValueReader.RemoveByTagAsync("missing-tag", cancellationToken).ConfigureAwait(false);
        await nullValueReader.RemoveKeysByPatternAsync("*", cancellationToken).ConfigureAwait(false);
        checks++;

        using var missingRegionMemory = new MemoryCache(new MemoryCacheOptions());
        using var missingRegionNearCache = new KyrolusRedisNearCacheProvider(
            missingRegionMemory,
            primaryConnection,
            new KyrolusRedisCacheDependencies(
                new KyrolusJsonCacheSerializer(),
                new KyrolusCacheKeyFactory($"{nearPrefix}:missing-region"),
                CreateRedisRuntimeOptions($"{nearPrefix}:missing-region", "diag-near", tenantId),
                KyrolusNullCacheObserver.Instance,
                KyrolusNullCachePolicyProvider.Instance),
            new KyrolusRedisNearCacheOptions
            {
                InvalidationChannel = $"{nearChannel}:missing-region",
                PublishInvalidations = false,
                SubscribeInvalidations = false
            },
            invalidationBus: null);
        await ExpectThrowsAsync<InvalidOperationException>(
            () => missingRegionNearCache.GetOrCreateAsync(
                "missing-region",
                _ => Task.FromResult("value"),
                new KyrolusCacheEntryOptions
                {
                    Region = " ",
                    TenantId = tenantId
                },
                cancellationToken)).ConfigureAwait(false);
        checks++;

        using var missingTenantMemory = new MemoryCache(new MemoryCacheOptions());
        using var missingTenantNearCache = new KyrolusRedisNearCacheProvider(
            missingTenantMemory,
            primaryConnection,
            new KyrolusRedisCacheDependencies(
                new KyrolusJsonCacheSerializer(),
                new KyrolusCacheKeyFactory($"{nearPrefix}:missing-tenant"),
                CreateRedisRuntimeOptions($"{nearPrefix}:missing-tenant", "diag-near", tenantId),
                KyrolusNullCacheObserver.Instance,
                KyrolusNullCachePolicyProvider.Instance),
            new KyrolusRedisNearCacheOptions
            {
                InvalidationChannel = $"{nearChannel}:missing-tenant",
                PublishInvalidations = false,
                SubscribeInvalidations = false
            },
            invalidationBus: null);
        await ExpectThrowsAsync<InvalidOperationException>(
            () => missingTenantNearCache.GetOrCreateAsync(
                "missing-tenant",
                _ => Task.FromResult("value"),
                new KyrolusCacheEntryOptions
                {
                    Region = "diag-near",
                    TenantId = " "
                },
                cancellationToken)).ConfigureAwait(false);
        checks++;

        var fallbackOptions = CreateRedisRuntimeOptions($"{prefix}:faults", region, tenantId);
        fallbackOptions.EnableGracefulFallback = true;

        var fallbackRecorder = new RedisRuntimeObserver();
        var fallbackSeedCache = BuildRedisCacheProvider(primaryConnection, fallbackOptions, observer: fallbackRecorder);
        await fallbackSeedCache.SetAsync(
            "deserialize-fault",
            "value",
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken).ConfigureAwait(false);

        var deserializeFaultCache = BuildRedisCacheProvider(
            primaryConnection,
            fallbackOptions,
            observer: fallbackRecorder,
            serializer: new ThrowingRedisCacheSerializer(throwOnDeserialize: true));
        Require(
            await deserializeFaultCache.GetAsync<string>("deserialize-fault", cancellationToken).ConfigureAwait(false) is null &&
            fallbackRecorder.ErrorObservations.Any(context =>
                context.Operation == KyrolusCacheOperation.Get &&
                string.Equals(context.Key, "deserialize-fault", StringComparison.Ordinal)),
            "Redis cache runtime should gracefully fall back when deserialization throws a Redis exception.",
            ref checks);

        var serializeRecorder = new RedisRuntimeObserver();
        var serializeFaultCache = BuildRedisCacheProvider(
            primaryConnection,
            fallbackOptions,
            observer: serializeRecorder,
            serializer: new ThrowingRedisCacheSerializer(throwOnSerialize: true));
        await serializeFaultCache.SetAsync(
            "serialize-fault",
            "value",
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken).ConfigureAwait(false);
        Require(
            !await fallbackSeedCache.ExistsAsync("serialize-fault", cancellationToken).ConfigureAwait(false) &&
            serializeRecorder.ErrorObservations.Any(context =>
                context.Operation == KyrolusCacheOperation.Set &&
                string.Equals(context.Key, "serialize-fault", StringComparison.Ordinal)),
            "Redis cache runtime should gracefully fall back when serialization throws during single-key writes.",
            ref checks);

        await serializeFaultCache.SetManyAsync(
            [
                new KeyValuePair<string, string>("serialize-many-a", "1"),
                new KeyValuePair<string, string>("serialize-many-b", "2")
            ],
            TimeSpan.FromMinutes(1),
            cancellationToken).ConfigureAwait(false);
        await serializeFaultCache.SetManyAsync(
            [
                new KeyValuePair<string, string>("serialize-many-c", "3")
            ],
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken).ConfigureAwait(false);
        Require(
            !await fallbackSeedCache.ExistsAsync("serialize-many-a", cancellationToken).ConfigureAwait(false) &&
            !await fallbackSeedCache.ExistsAsync("serialize-many-b", cancellationToken).ConfigureAwait(false) &&
            !await fallbackSeedCache.ExistsAsync("serialize-many-c", cancellationToken).ConfigureAwait(false) &&
            serializeRecorder.ErrorObservations.Count(context => context.Operation == KyrolusCacheOperation.SetMany) >= 2,
            "Redis cache runtime should gracefully fall back when serialization throws during batched writes.",
            ref checks);

        await fallbackSeedCache.SetManyAsync(
            [
                new KeyValuePair<string, string>("observer-a", "1"),
                new KeyValuePair<string, string>("observer-b", "2")
            ],
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
                Tags = ["observer-tag"]
            },
            cancellationToken).ConfigureAwait(false);

        var observerRecorder = new RedisRuntimeObserver();
        var observerFaultCache = BuildRedisCacheProvider(
            primaryConnection,
            fallbackOptions,
            observer: new ThrowingRedisCacheObserver(
                observerRecorder,
                context => context.Observation != KyrolusCacheObservation.Error));
        Require(
            !await observerFaultCache.ExistsAsync("observer-a", cancellationToken).ConfigureAwait(false) &&
            observerRecorder.ErrorObservations.Any(context => context.Operation == KyrolusCacheOperation.Exists),
            "Redis cache runtime should gracefully fall back when observer callbacks fail during exists checks.",
            ref checks);

        var observerMany = await observerFaultCache.GetManyAsync<string>(["observer-a", "observer-b"], cancellationToken).ConfigureAwait(false);
        Require(
            observerMany.Count == 0 &&
            observerRecorder.ErrorObservations.Any(context => context.Operation == KyrolusCacheOperation.GetMany),
            "Redis cache runtime should gracefully fall back when observer callbacks fail during batched reads.",
            ref checks);

        await observerFaultCache.RemoveAsync("observer-a", cancellationToken).ConfigureAwait(false);
        await observerFaultCache.RemoveManyAsync(["observer-b"], cancellationToken).ConfigureAwait(false);
        await observerFaultCache.RemoveByTagAsync("observer-tag", cancellationToken).ConfigureAwait(false);
        await observerFaultCache.RemoveKeysByPatternAsync("observer-*", cancellationToken).ConfigureAwait(false);
        Require(
            observerRecorder.ErrorObservations.Any(context => context.Operation == KyrolusCacheOperation.Remove) &&
            observerRecorder.ErrorObservations.Any(context => context.Operation == KyrolusCacheOperation.RemoveMany) &&
            observerRecorder.ErrorObservations.Any(context => context.Operation == KyrolusCacheOperation.RemoveByTag) &&
            observerRecorder.ErrorObservations.Any(context => context.Operation == KyrolusCacheOperation.RemoveByPattern),
            "Redis cache runtime should gracefully fall back when observer callbacks fail during invalidation operations.",
            ref checks);

        var observerFactoryCalls = 0;
        var observerFactoryResult = await observerFaultCache.GetOrCreateAsync(
            "observer-factory",
            _ =>
            {
                observerFactoryCalls++;
                return Task.FromResult("observer-created");
            },
            new KyrolusCacheEntryOptions
            {
                Region = region,
                TenantId = tenantId,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            },
            cancellationToken).ConfigureAwait(false);
        Require(
            observerFactoryResult == "observer-created" &&
            observerFactoryCalls == 1 &&
            observerRecorder.ErrorObservations.Any(context => context.Operation == KyrolusCacheOperation.GetOrCreate),
            "Redis cache runtime should gracefully fall back to the factory when observer callbacks fail during GetOrCreate.",
            ref checks);

        Require(logEntries.Any(entry => entry.Contains("Cache", StringComparison.Ordinal)), "Redis cache runtime should emit cache observer logs.", ref checks);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "redis-cache-runtime",
            RedisCacheChecks: checks);
    }

}

