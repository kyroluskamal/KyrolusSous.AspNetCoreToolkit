using System.Collections.Concurrent;
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
        var cache = provider.GetRequiredService<ICacheProvider>();
        var options = provider.GetRequiredService<KyrolusRedisCacheOptions>();
        var keyFactory = provider.GetRequiredService<IKyrolusCacheKeyFactory>();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();

        Require(cache is RedisCacheProvider, "Redis cache runtime should resolve the Redis cache provider.", ref checks);
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
        var extensionCache = extensionProvider.GetRequiredService<ICacheProvider>();
        var extensionSerializer = extensionProvider.GetRequiredService<IKyrolusCacheSerializer>();
        Require(
            extensionCache is RedisCacheProvider &&
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
        var scanCache = new RedisCacheProvider(
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
        var nearCacheA = nearProviderA.GetRequiredService<ICacheProvider>();
        var nearCacheB = nearProviderB.GetRequiredService<ICacheProvider>();
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
        var noPublishNearCache = noPublishNearProvider.GetRequiredService<ICacheProvider>();
        await noPublishNearCache.SetAsync("nopublish", "value", TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        await noPublishNearCache.RemoveAsync("nopublish", cancellationToken).ConfigureAwait(false);
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
            nearConnectionStringProvider.GetRequiredService<ICacheProvider>() is KyrolusRedisNearCacheProvider,
            "Redis near cache runtime should support the connection-string registration overload.",
            ref checks);

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

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunRedisFallbackRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var checks = 0;

        using var disconnectedConnection = await CreateDisconnectedRedisConnectionAsync().ConfigureAwait(false);
        var observer = new RedisRuntimeObserver();
        var gracefulOptions = new KyrolusRedisCacheOptions
        {
            KeyPrefix = $"kyrolus:diag:fallback:{Guid.NewGuid():N}",
            DefaultRegion = "fallback",
            DefaultTenantId = "fallback-tenant",
            RequireRegion = true,
            RequireTenantId = true,
            EnableGracefulFallback = true,
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions
            {
                Enabled = true,
                FailureThreshold = 1,
                OpenDuration = TimeSpan.FromSeconds(10),
                ThrowOnOpen = false
            }
        };

        var gracefulCache = new RedisCacheProvider(
            disconnectedConnection,
            new KyrolusRedisCacheDependencies(
                new KyrolusJsonCacheSerializer(),
                new KyrolusCacheKeyFactory(gracefulOptions.KeyPrefix),
                gracefulOptions,
                observer,
                KyrolusNullCachePolicyProvider.Instance));
        Require(await gracefulCache.GetAsync<string>("missing", cancellationToken).ConfigureAwait(false) is null, "Redis fallback runtime should return default values for missing reads.", ref checks);
        await gracefulCache.SetAsync("alpha", "value", TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        Require(!await gracefulCache.ExistsAsync("alpha", cancellationToken).ConfigureAwait(false), "Redis fallback runtime should return false for exists when Redis is unavailable.", ref checks);

        var fallbackMany = await gracefulCache.GetManyAsync<string>(["one", "two"], cancellationToken).ConfigureAwait(false);
        Require(
            fallbackMany.Count == 2 && fallbackMany.Values.All(value => value is null),
            "Redis fallback runtime should return default values for batched reads.",
            ref checks);

        await gracefulCache.SetManyAsync(
            [new KeyValuePair<string, string>("one", "1")],
            TimeSpan.FromMinutes(1),
            cancellationToken).ConfigureAwait(false);
        await gracefulCache.RemoveAsync("alpha", cancellationToken).ConfigureAwait(false);
        await gracefulCache.RemoveManyAsync(["one"], cancellationToken).ConfigureAwait(false);
        await gracefulCache.RemoveByTagAsync("tag", cancellationToken).ConfigureAwait(false);
        await gracefulCache.RemoveKeysByPatternAsync("pattern-*", cancellationToken).ConfigureAwait(false);
        checks++;

        var fallbackFactoryCalls = 0;
        var fallbackValue = await gracefulCache.GetOrCreateAsync(
            "factory",
            _ =>
            {
                fallbackFactoryCalls++;
                return Task.FromResult("factory-value");
            },
            null,
            cancellationToken).ConfigureAwait(false);
        Require(
            fallbackValue == "factory-value" && fallbackFactoryCalls == 1,
            "Redis fallback runtime should fall back to the provided factory.",
            ref checks);
        Require(observer.ErrorObservations.Count >= 6, "Redis fallback runtime should observe graceful fallback errors.", ref checks);
        Require(
            (await gracefulCache.GetManyAsync<string>(Array.Empty<string>(), cancellationToken).ConfigureAwait(false)).Count == 0,
            "Redis fallback runtime should preserve empty batched reads when Redis is unavailable.",
            ref checks);
        await gracefulCache.SetAsync("options-alpha", "value", new KyrolusCacheEntryOptions
        {
            Region = "fallback",
            TenantId = "fallback-tenant"
        }, cancellationToken).ConfigureAwait(false);
        await gracefulCache.SetManyAsync(Array.Empty<KeyValuePair<string, string>>(), new KyrolusCacheEntryOptions
        {
            Region = "fallback",
            TenantId = "fallback-tenant"
        }, cancellationToken).ConfigureAwait(false);
        await gracefulCache.RemoveManyAsync(Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        checks++;

        var throwingOptions = new KyrolusRedisCacheOptions
        {
            KeyPrefix = $"kyrolus:diag:fallback:throw:{Guid.NewGuid():N}",
            DefaultRegion = "fallback",
            DefaultTenantId = "fallback-tenant",
            RequireRegion = true,
            RequireTenantId = true,
            EnableGracefulFallback = true,
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions
            {
                Enabled = true,
                FailureThreshold = 1,
                OpenDuration = TimeSpan.FromMinutes(1),
                ThrowOnOpen = true
            }
        };
        var throwingCache = new RedisCacheProvider(
            disconnectedConnection,
            new KyrolusRedisCacheDependencies(
                new KyrolusJsonCacheSerializer(),
                new KyrolusCacheKeyFactory(throwingOptions.KeyPrefix),
                throwingOptions,
                KyrolusNullCacheObserver.Instance,
                KyrolusNullCachePolicyProvider.Instance));
        _ = await throwingCache.GetAsync<string>("first", cancellationToken).ConfigureAwait(false);
        await ExpectThrowsAsync<KyrolusRedisCircuitOpenException>(
            () => throwingCache.GetAsync<string>("second", cancellationToken),
            "Redis fallback runtime should throw when the circuit is open and ThrowOnOpen is enabled.").ConfigureAwait(false);
        checks++;

        var unhealthyResult = await new KyrolusRedisCacheHealthCheck(
            disconnectedConnection,
            new KyrolusRedisCacheHealthCheckOptions
            {
                FailureStatus = HealthStatus.Degraded,
                IncludeLatency = false
            }).CheckHealthAsync(new HealthCheckContext(), cancellationToken).ConfigureAwait(false);
        Require(unhealthyResult.Status == HealthStatus.Degraded, "Redis fallback runtime should report unhealthy connections.", ref checks);

        var openException = new KyrolusRedisCircuitOpenException(TimeSpan.FromSeconds(3));
        Require(
            openException.RetryAfter == TimeSpan.FromSeconds(3) &&
            openException.Message.Contains("Retry after", StringComparison.Ordinal),
            "Redis circuit open exceptions should expose retry metadata.",
            ref checks);

        RunRedisValidatorScenarios(ref checks);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "redis-fallback-runtime",
            RedisFallbackChecks: checks);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunDataProtectionRedisRuntimeAsync(
        string redisConnectionString,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var unique = Guid.NewGuid().ToString("N");
        var applicationName = $"diag-dataprotection-redis-{unique}";
        var key = $"kyrolus:diag:dataprotection:{unique}";
        var stringKey = $"{key}:string";
        var channel = $"kyrolus:diag:dataprotection:{unique}:channel";

        using var publisherConnection = await ConnectRedisAsync(redisConnectionString).ConfigureAwait(false);
        using var listenerConnection = await ConnectRedisAsync(redisConnectionString).ConfigureAwait(false);

        try
        {
            var publisherServices = new ServiceCollection();
            publisherServices.AddLogging();
            var publisherBuilder = publisherServices.AddKyrolusDataProtection(options =>
            {
                options.ApplicationName = applicationName;
                options.DefaultKeyLifetime = TimeSpan.FromDays(7);
            });
            publisherBuilder
                .AddKyrolusDataProtectionRedis(publisherConnection, key)
                .AddKyrolusDataProtectionRedisKeyRingRefreshNotifications(options =>
                {
                    options.Channel = channel;
                    options.IncludeApplicationNameInChannel = true;
                });

            using var publisherProvider = publisherServices.BuildServiceProvider();
            var keyManager = publisherProvider.GetRequiredService<IKyrolusDataProtectionKeyManager>();
            var tenantProvider = publisherProvider.GetRequiredService<IKyrolusTenantDataProtectionProvider>();
            var notifier = publisherProvider.GetRequiredService<IKyrolusKeyRingRefreshNotifier>();
            var refreshOptions = publisherProvider.GetRequiredService<IOptions<KyrolusDataProtectionKeyRingRefreshOptions>>().Value;

            Require(
                refreshOptions.EnableCrossInstanceNotifications &&
                refreshOptions.RefreshOnExternalSignal &&
                refreshOptions.PublishLocalChanges,
                "Redis data protection runtime should enable key-ring refresh integration flags.",
                ref checks);

            var createdKey = await keyManager.CreateKeyAsync(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                TimeSpan.FromDays(7),
                cancellationToken).ConfigureAwait(false);
            Require(createdKey.KeyId != Guid.Empty, "Redis data protection runtime should create keys through the Redis-backed repository.", ref checks);
            Require(
                await publisherConnection.GetDatabase().KeyExistsAsync(key).ConfigureAwait(false),
                "Redis data protection runtime should persist keys in Redis.",
                ref checks);

            var protector = tenantProvider.CreateProtector(tenantId, "redis-runtime");
            var protectedPayload = protector.Protect(Encoding.UTF8.GetBytes("redis-payload"));
            var unprotectedPayload = protector.Unprotect(protectedPayload);
            Require(
                Encoding.UTF8.GetString(unprotectedPayload) == "redis-payload",
                "Redis data protection runtime should round-trip tenant-scoped payloads.",
                ref checks);

            var stringServices = new ServiceCollection();
            stringServices.AddLogging();
            var stringBuilder = stringServices.AddKyrolusDataProtection(options =>
            {
                options.ApplicationName = $"{applicationName}-string";
            });
            stringBuilder.AddKyrolusDataProtectionRedis(redisConnectionString, stringKey);
            using var stringProvider = stringServices.BuildServiceProvider();
            var stringKeyManager = stringProvider.GetRequiredService<IKyrolusDataProtectionKeyManager>();
            var rotatedKey = await stringKeyManager.RotateKeyAsync(TimeSpan.FromDays(2), cancellationToken).ConfigureAwait(false);
            Require(rotatedKey.KeyId != Guid.Empty, "Redis data protection runtime should support the connection-string Redis registration overload.", ref checks);
            Require(
                await publisherConnection.GetDatabase().KeyExistsAsync(stringKey).ConfigureAwait(false),
                "Redis data protection runtime should persist keys for the connection-string registration path.",
                ref checks);
            var listenerServices = new ServiceCollection();
            listenerServices.AddLogging();
            var listenerBuilder = listenerServices.AddKyrolusDataProtection(options =>
            {
                options.ApplicationName = applicationName;
            });
            listenerBuilder
                .AddKyrolusDataProtectionRedis(listenerConnection, key)
                .AddKyrolusDataProtectionRedisKeyRingRefreshNotifications(options =>
                {
                    options.Channel = channel;
                    options.IncludeApplicationNameInChannel = true;
                });

            using var listenerProvider = listenerServices.BuildServiceProvider();
            var listenerNotifier = listenerProvider.GetRequiredService<IKyrolusKeyRingRefreshNotifier>();

            var validSignal = await ListenForKeyRingSignalAsync(
                listenerNotifier,
                async () =>
                {
                    await notifier.PublishAsync(
                        new KyrolusKeyRingRefreshSignal(
                            applicationName,
                            "publisher-instance",
                            DateTimeOffset.UtcNow,
                            KyrolusKeyRingRefreshReason.KeyRotated),
                        cancellationToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            Require(
                validSignal.InstanceId == "publisher-instance" &&
                validSignal.Reason == KyrolusKeyRingRefreshReason.KeyRotated,
                "Redis data protection runtime should publish and receive key-ring refresh notifications.",
                ref checks);

            var rawChannel = RedisChannel.Literal(BuildKeyRingChannel(channel, applicationName));
            var unknownSignal = await ListenForKeyRingSignalAsync(
                listenerNotifier,
                async () =>
                {
                    await publisherConnection.GetSubscriber().PublishAsync(rawChannel, "bad-payload").ConfigureAwait(false);
                    var payload = string.Join(
                        '|',
                        Uri.EscapeDataString(applicationName),
                        Uri.EscapeDataString("raw-instance"),
                        DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture),
                        "999");
                    await publisherConnection.GetSubscriber().PublishAsync(rawChannel, payload).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            Require(
                unknownSignal.InstanceId == "raw-instance" &&
                unknownSignal.Reason == KyrolusKeyRingRefreshReason.Unknown,
                "Redis data protection runtime should ignore malformed messages and map unknown reasons.",
                ref checks);

            ExpectThrows<ArgumentNullException>(
                () => KyrolusSous.DataProtection.Redis.ServiceCollectionExtensions.AddKyrolusDataProtectionRedis(null!, publisherConnection),
                "Redis data protection runtime should reject null builders for the connection overload.",
                ref checks);
            ExpectThrows<ArgumentNullException>(
                () => KyrolusSous.DataProtection.Redis.ServiceCollectionExtensions.AddKyrolusDataProtectionRedis(publisherBuilder, (IConnectionMultiplexer)null!, key),
                "Redis data protection runtime should reject null connections.",
                ref checks);
            ExpectThrows<ArgumentException>(
                () => KyrolusSous.DataProtection.Redis.ServiceCollectionExtensions.AddKyrolusDataProtectionRedis(publisherBuilder, publisherConnection, " "),
                "Redis data protection runtime should reject blank Redis key names.",
                ref checks);
            ExpectThrows<ArgumentException>(
                () => KyrolusSous.DataProtection.Redis.ServiceCollectionExtensions.AddKyrolusDataProtectionRedis(publisherBuilder, " ", key),
                "Redis data protection runtime should reject blank connection strings.",
                ref checks);
            await ExpectThrowsAsync<ArgumentNullException>(
                () => notifier.ListenAsync(null!, cancellationToken),
                "Redis data protection runtime should reject null listeners.").ConfigureAwait(false);
            checks++;

            return new RepositoryRuntimeDiagnosticsResponse(
                Mode: "data-protection-redis-runtime",
                DataProtectionRedisChecks: checks);
        }
        finally
        {
            await publisherConnection.GetDatabase().KeyDeleteAsync((RedisKey)key).ConfigureAwait(false);
            await publisherConnection.GetDatabase().KeyDeleteAsync((RedisKey)stringKey).ConfigureAwait(false);
        }
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunExceptionAbstractionsRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var context = new KyrolusErrorContext(
            TraceId: "trace-1",
            CorrelationId: "correlation-1",
            UserId: "user-1",
            TenantId: "tenant-1",
            Path: "/api/diagnostics/exception",
            Method: "GET",
            Culture: CultureInfo.InvariantCulture);

        var code = $"diag_{Guid.NewGuid():N}";
        var definition = new KyrolusErrorCodeDefinition(code, "Diagnostics title", HttpStatusCode.Accepted, "Diagnostics description");
        KyrolusErrorCodeRegistry.Register(definition);
        Require(KyrolusErrorCodeRegistry.IsValidCode(code), "Exception abstractions runtime should validate registered error codes.", ref checks);
        Require(
            KyrolusErrorCodeRegistry.TryGet(code, out var fetchedDefinition) &&
            fetchedDefinition == definition,
            "Exception abstractions runtime should resolve registered error codes.",
            ref checks);
        Require(
            KyrolusErrorCodeRegistry.Snapshot().Any(item => item.Code == KyrolusErrorCodes.InternalError) &&
            KyrolusErrorCodeRegistry.Snapshot().Any(item => item.Code == code),
            "Exception abstractions runtime should expose registered error code snapshots.",
            ref checks);
        ExpectThrows<KyrolusErrorCodeRegistryException>(
            () => KyrolusErrorCodeRegistry.Register(definition),
            "Exception abstractions runtime should reject duplicate registrations.",
            ref checks);
        ExpectThrows<KyrolusErrorCodeRegistryException>(
            () => KyrolusErrorCodeRegistry.Register(new KyrolusErrorCodeDefinition("Invalid-Code", "Invalid", HttpStatusCode.BadRequest)),
            "Exception abstractions runtime should reject invalid code formats.",
            ref checks);
        ExpectThrows<KyrolusErrorCodeRegistryException>(
            () => KyrolusErrorCodeRegistry.Register(new KyrolusErrorCodeDefinition(" ", "Blank", HttpStatusCode.BadRequest)),
            "Exception abstractions runtime should reject blank code values.",
            ref checks);

        var rangeCode = $"diag_{Guid.NewGuid():N}";
        KyrolusErrorCodeRegistry.RegisterRange(
        [
            new KyrolusErrorCodeDefinition(rangeCode, "Range title", HttpStatusCode.Created)
        ]);
        Require(KyrolusErrorCodeRegistry.TryGet(rangeCode, out _), "Exception abstractions runtime should register ranges of codes.", ref checks);

        var validationErrors = new[] { new KyrolusErrorItem("Name", "name.required", "Name is required") };
        var notFound = new KyrolusNotFoundException("MenuItem", "42");
        var badRequest = new KyrolusBadRequestException("Bad request", "bad-detail");
        var conflict = new KyrolusConflictException("Conflict", "conflict-detail");
        var forbidden = new KyrolusForbiddenException("forbidden-detail");
        var unauthorized = new KyrolusUnauthorizedException("unauthorized-detail");
        var timeout = new KyrolusTimeoutException("timeout-detail");
        var rateLimit = new KyrolusRateLimitException("rate-limit-detail");
        var externalService = new KyrolusExternalServiceException("redis", "redis-detail");
        var validation = new KyrolusValidationException(validationErrors, "Validation title", "validation-detail");

        Require(notFound.EntityName == "MenuItem" && notFound.Key == "42" && notFound.StatusCode == HttpStatusCode.NotFound, "Exception abstractions runtime should preserve KyrolusNotFoundException metadata.", ref checks);
        Require(badRequest.Code == KyrolusErrorCodes.BadRequest && badRequest.Detail == "bad-detail", "Exception abstractions runtime should preserve bad-request metadata.", ref checks);
        Require(conflict.StatusCode == HttpStatusCode.Conflict && conflict.Detail == "conflict-detail", "Exception abstractions runtime should preserve conflict metadata.", ref checks);
        Require(forbidden.StatusCode == HttpStatusCode.Forbidden, "Exception abstractions runtime should preserve forbidden metadata.", ref checks);
        Require(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Exception abstractions runtime should preserve unauthorized metadata.", ref checks);
        Require(timeout.IsTransient && timeout.StatusCode == HttpStatusCode.GatewayTimeout, "Exception abstractions runtime should mark timeout exceptions as transient.", ref checks);
        Require(rateLimit.IsTransient && rateLimit.StatusCode == (HttpStatusCode)429, "Exception abstractions runtime should mark rate-limit exceptions as transient.", ref checks);
        Require(externalService.ServiceName == "redis" && externalService.IsTransient, "Exception abstractions runtime should preserve external service metadata.", ref checks);
        Require(validation.Errors?.Count == 1 && validation.Code == KyrolusErrorCodes.Validation, "Exception abstractions runtime should preserve validation failures.", ref checks);

        var envelope = new KyrolusErrorEnvelope(
            code,
            "Envelope title",
            "Envelope detail",
            context.TraceId,
            validationErrors,
            new Dictionary<string, object?> { ["tenant"] = context.TenantId });
        var mapping = new KyrolusExceptionMapping(envelope, HttpStatusCode.Conflict, IsTransient: true, ShouldLog: false);
        var result = new KyrolusErrorResult(envelope, HttpStatusCode.Conflict, IsTransient: true, ExceptionType: typeof(KyrolusConflictException).FullName);
        Require(mapping.IsTransient && !mapping.ShouldLog, "Exception abstractions runtime should preserve mapping metadata.", ref checks);
        Require(result.StatusCode == HttpStatusCode.Conflict && result.ExceptionType == typeof(KyrolusConflictException).FullName, "Exception abstractions runtime should preserve error results.", ref checks);

        var redisMapper = new KyrolusRedisExceptionMapper();
        Require(redisMapper.Order == -60, "Exception abstractions runtime should preserve Redis mapper ordering.", ref checks);
        Require(
            redisMapper.TryMap(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis down"), context, out var timeoutMapping) &&
            timeoutMapping.StatusCode == HttpStatusCode.GatewayTimeout &&
            timeoutMapping.Error.Code == KyrolusErrorCodes.Timeout,
            "Exception abstractions runtime should map Redis connection failures to timeout envelopes.",
            ref checks);
        Require(
            redisMapper.TryMap(new RedisServerException("redis server error"), context, out var externalMapping) &&
            externalMapping.StatusCode == HttpStatusCode.BadGateway &&
            externalMapping.Error.Code == KyrolusErrorCodes.ExternalService,
            "Exception abstractions runtime should map Redis server failures to external service envelopes.",
            ref checks);
        Require(
            !redisMapper.TryMap(new InvalidOperationException("not redis"), context, out _),
            "Exception abstractions runtime should decline unsupported exceptions.",
            ref checks);

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "exception-abstractions-runtime",
            ExceptionAbstractionsChecks: checks);
    }
    private static async Task<ConnectionMultiplexer> ConnectRedisAsync(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = Math.Max(1, options.ConnectRetry);
        options.ConnectTimeout = options.ConnectTimeout <= 0 ? 5000 : options.ConnectTimeout;
        options.SyncTimeout = options.SyncTimeout <= 0 ? 5000 : options.SyncTimeout;
        return await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
    }

    private static async Task<ConnectionMultiplexer> CreateDisconnectedRedisConnectionAsync()
    {
        foreach (var candidate in new[] { "127.0.0.1:6399", "127.0.0.1:1" })
        {
            var options = ConfigurationOptions.Parse(candidate);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 0;
            options.ConnectTimeout = 500;
            options.SyncTimeout = 500;

            var connection = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
            if (!connection.IsConnected)
            {
                return connection;
            }

            connection.Dispose();
        }

        throw new InvalidOperationException("Unable to create a disconnected Redis connection for fallback diagnostics.");
    }

    private static RedisCacheProvider BuildRedisCacheProvider(
        IConnectionMultiplexer connection,
        KyrolusRedisCacheOptions options,
        IKyrolusCacheObserver? observer = null,
        IKyrolusCachePolicyProvider? policyProvider = null,
        IKyrolusCacheSerializer? serializer = null)
    {
        return new RedisCacheProvider(
            connection,
            new KyrolusRedisCacheDependencies(
                serializer ?? new KyrolusJsonCacheSerializer(),
                new KyrolusCacheKeyFactory(options.KeyPrefix),
                options,
                observer,
                policyProvider));
    }

    private static KyrolusRedisCacheOptions CreateRedisRuntimeOptions(
        string prefix,
        string region,
        string tenantId)
    {
        return new KyrolusRedisCacheOptions
        {
            KeyPrefix = prefix,
            DefaultRegion = region,
            DefaultTenantId = tenantId,
            RequireRegion = true,
            RequireTenantId = true,
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false }
        };
    }

    private static object CreateRedisCircuitBreaker(KyrolusRedisCircuitBreakerOptions options)
    {
        var type = typeof(KyrolusRedisCacheOptions).Assembly.GetType("KyrolusSous.Caching.Redis.KyrolusRedisCircuitBreaker", throwOnError: true)!;
        return Activator.CreateInstance(type, options)!;
    }

    private static bool TryEnterRedisCircuitBreaker(object circuitBreaker, out TimeSpan? retryAfter)
    {
        var arguments = new object?[] { null };
        var tryEnter = (bool)circuitBreaker.GetType().GetMethod("TryEnter")!.Invoke(circuitBreaker, arguments)!;
        retryAfter = arguments[0] is TimeSpan value ? value : null;
        return tryEnter;
    }

    private static void ReportRedisCircuitBreakerSuccess(object circuitBreaker)
        => circuitBreaker.GetType().GetMethod("ReportSuccess")!.Invoke(circuitBreaker, null);

    private static void ReportRedisCircuitBreakerFailure(object circuitBreaker)
        => circuitBreaker.GetType().GetMethod("ReportFailure")!.Invoke(circuitBreaker, null);

    private static ServiceProvider BuildNearCacheServiceProvider(
        IConnectionMultiplexer connection,
        string prefix,
        string tenantId,
        string channel,
        Action<KyrolusRedisCacheOptions>? configureCache = null,
        Action<KyrolusRedisNearCacheOptions>? configureNearCache = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddKyrolusRedisNearCache(
            configure: options =>
            {
                options.KeyPrefix = prefix;
                options.DefaultRegion = "diag-near";
                options.DefaultTenantId = tenantId;
                options.RequireRegion = true;
                options.RequireTenantId = true;
                options.CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = false };
                configureCache?.Invoke(options);
            },
            configureNearCache: options =>
            {
                options.InvalidationChannel = channel;
                options.DefaultL1Ttl = TimeSpan.FromMinutes(1);
                options.PublishInvalidations = true;
                options.SubscribeInvalidations = true;
                configureNearCache?.Invoke(options);
            });
        return services.BuildServiceProvider();
    }

    private static async Task CleanupRedisPrefixAsync(
        IConnectionMultiplexer connection,
        string prefix,
        CancellationToken cancellationToken)
    {
        var database = connection.GetDatabase();
        foreach (var endpoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endpoint);
            if (!server.IsConnected || !server.Features.Scan)
            {
                continue;
            }

            var keys = server.Keys(database.Database, pattern: $"{prefix}*", pageSize: 256).ToArray();
            if (keys.Length == 0)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await database.KeyDeleteAsync(keys).ConfigureAwait(false);
        }
    }

    private static async Task<bool> AwaitConditionAsync(
        Func<Task<bool>> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await predicate().ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return await predicate().ConfigureAwait(false);
    }

    private static async Task<KyrolusKeyRingRefreshSignal> ListenForKeyRingSignalAsync(
        IKyrolusKeyRingRefreshNotifier notifier,
        Func<Task> publish,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var signalSource = new TaskCompletionSource<KyrolusKeyRingRefreshSignal>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listenTask = Task.Run(
            () => notifier.ListenAsync(
                (signal, _) =>
                {
                    signalSource.TrySetResult(signal);
                    return Task.CompletedTask;
                },
                linkedCts.Token),
            CancellationToken.None);

        await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        await publish().ConfigureAwait(false);
        var signal = await signalSource.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

        linkedCts.Cancel();
        await SwallowCancellationAsync(listenTask).ConfigureAwait(false);
        return signal;
    }

    private static async Task SwallowCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string BuildKeyRingChannel(string baseChannel, string applicationName)
        => $"{baseChannel}:{applicationName}";

    private static byte[] CreateDiagnosticsIv()
        => Enumerable.Range(101, 16).Select(static value => (byte)value).ToArray();

    private static void RunRedisValidatorScenarios(ref int checks)
    {
        Require(
            KyrolusRedisCacheDependencies.Default.Serializer is not null &&
            KyrolusRedisCacheDependencies.Default.KeyFactory is not null &&
            KyrolusRedisCacheDependencies.Default.Options is not null,
            "Redis validator scenarios should expose default dependency values.",
            ref checks);

        KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions
        {
            BatchSize = 4,
            DefaultTtl = TimeSpan.FromMinutes(1),
            DefaultSlidingTtl = TimeSpan.FromSeconds(10),
            DefaultNegativeTtl = TimeSpan.FromSeconds(5),
            LockTtl = TimeSpan.FromSeconds(2),
            LockWait = TimeSpan.Zero,
            LockRetryDelay = TimeSpan.FromMilliseconds(10),
            EnableCompression = true,
            CompressionThresholdBytes = 16,
            EnableEncryption = true,
            EncryptionKey = CreateDiagnosticsAesKey(),
            EncryptionIv = CreateDiagnosticsIv(),
            RequireRegion = true,
            DefaultRegion = "diag",
            RequireTenantId = true,
            DefaultTenantId = "tenant",
            CircuitBreaker = new KyrolusRedisCircuitBreakerOptions
            {
                Enabled = true,
                FailureThreshold = 1,
                OpenDuration = TimeSpan.FromSeconds(1),
                MaxOpenDuration = TimeSpan.FromSeconds(5),
                HalfOpenSuccesses = 1
            }
        });
        checks++;

        ExpectThrows<ArgumentOutOfRangeException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions { BatchSize = 0 }),
            "Redis validator scenarios should reject non-positive batch sizes.",
            ref checks);
        ExpectThrows<ArgumentOutOfRangeException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions { EnableCompression = true, CompressionThresholdBytes = 0 }),
            "Redis validator scenarios should reject invalid compression thresholds.",
            ref checks);
        ExpectThrows<ArgumentException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions { EnableEncryption = true }),
            "Redis validator scenarios should require encryption keys when encryption is enabled.",
            ref checks);
        ExpectThrows<ArgumentException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions { EnableEncryption = true, EncryptionKey = [1, 2, 3] }),
            "Redis validator scenarios should reject invalid encryption key sizes.",
            ref checks);
        ExpectThrows<ArgumentException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions { EnableEncryption = true, EncryptionKeyBase64 = "bad-base64" }),
            "Redis validator scenarios should reject invalid encryption key Base64 payloads.",
            ref checks);
        ExpectThrows<ArgumentException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions
            {
                EnableEncryption = true,
                EncryptionKey = CreateDiagnosticsAesKey(),
                EncryptionIv = [1, 2, 3]
            }),
            "Redis validator scenarios should reject invalid IV sizes.",
            ref checks);
        ExpectThrows<InvalidOperationException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions { RequireRegion = true }),
            "Redis validator scenarios should require a default region when region isolation is enabled.",
            ref checks);
        ExpectThrows<InvalidOperationException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions { RequireTenantId = true }),
            "Redis validator scenarios should require a default tenant when tenant isolation is enabled.",
            ref checks);
        ExpectThrows<ArgumentOutOfRangeException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions
            {
                CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = true, FailureThreshold = 0 }
            }),
            "Redis validator scenarios should reject invalid circuit breaker thresholds.",
            ref checks);
        ExpectThrows<ArgumentOutOfRangeException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisCacheOptions
            {
                CircuitBreaker = new KyrolusRedisCircuitBreakerOptions { Enabled = true, BackoffMultiplier = 0.5 }
            }),
            "Redis validator scenarios should reject invalid circuit breaker multipliers.",
            ref checks);
        ExpectThrows<ArgumentException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisNearCacheOptions { InvalidationChannel = " " }),
            "Redis validator scenarios should reject blank near-cache invalidation channels.",
            ref checks);
        ExpectThrows<ArgumentException>(
            () => KyrolusRedisCacheOptionsValidator.Validate(new KyrolusRedisInvalidationOptions { Channel = " " }),
            "Redis validator scenarios should reject blank invalidation channels.",
            ref checks);
    }
}

internal sealed class RedisRuntimeObserver : IKyrolusCacheObserver
{
    public ConcurrentQueue<KyrolusCacheObserverContext> ErrorObservations { get; } = new();

    public Task OnObservationAsync(KyrolusCacheObserverContext context)
    {
        if (context.Observation == KyrolusCacheObservation.Error)
        {
            ErrorObservations.Enqueue(context);
        }

        return Task.CompletedTask;
    }
}

internal sealed class RuntimePrefixPayloadTransformer(string prefix) : IKyrolusCachePayloadTransformer
{
    private readonly byte[] prefixBytes = Encoding.UTF8.GetBytes(prefix);

    public byte[] Transform(byte[] payload) => [.. prefixBytes, .. payload];

    public byte[] Restore(byte[] payload) => payload[prefixBytes.Length..];
}

internal sealed class ThrowingRedisCacheSerializer(bool throwOnSerialize = false, bool throwOnDeserialize = false) : IKyrolusCacheSerializer
{
    private readonly KyrolusJsonCacheSerializer inner = new();

    public byte[] Serialize<T>(T value)
    {
        if (throwOnSerialize)
        {
            throw new RedisServerException("Simulated serializer write failure.");
        }

        return inner.Serialize(value);
    }

    public T? Deserialize<T>(byte[] payload)
    {
        if (throwOnDeserialize)
        {
            throw new RedisServerException("Simulated serializer read failure.");
        }

        return inner.Deserialize<T>(payload);
    }
}

internal sealed class ThrowingRedisCacheObserver(
    RedisRuntimeObserver recorder,
    Func<KyrolusCacheObserverContext, bool> shouldThrow) : IKyrolusCacheObserver
{
    private readonly RedisRuntimeObserver recorder = recorder;
    private readonly Func<KyrolusCacheObserverContext, bool> shouldThrow = shouldThrow;

    public Task OnObservationAsync(KyrolusCacheObserverContext context)
    {
        recorder.OnObservationAsync(context);
        if (shouldThrow(context))
        {
            throw new TimeoutException($"Simulated observer failure for {context.Operation}.");
        }

        return Task.CompletedTask;
    }
}

internal sealed class RuntimeCacheLoggerProvider(ConcurrentQueue<string> entries) : ILoggerProvider
{
    private readonly ConcurrentQueue<string> entries = entries;

    public ILogger CreateLogger(string categoryName) => new RuntimeCacheLogger(entries, categoryName);

    public void Dispose()
    {
    }
}

internal sealed class RuntimeCacheLogger(ConcurrentQueue<string> entries, string categoryName) : ILogger
{
    private readonly ConcurrentQueue<string> entries = entries;
    private readonly string categoryName = categoryName;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        entries.Enqueue($"{categoryName}|{logLevel}|{formatter(state, exception)}");
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
