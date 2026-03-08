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

        await nearCacheA.RemoveKeysByPatternAsync("bulk-*", cancellationToken).ConfigureAwait(false);
        var nearPatternInvalidated = await AwaitConditionAsync(
            async () =>
                await nearCacheB.GetAsync<string>("bulk-a", cancellationToken).ConfigureAwait(false) is null &&
                await nearCacheB.GetAsync<string>("bulk-b", cancellationToken).ConfigureAwait(false) is null,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
        Require(nearPatternInvalidated, "Redis near cache runtime should invalidate peer caches by pattern.", ref checks);

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

    private static ServiceProvider BuildNearCacheServiceProvider(
        IConnectionMultiplexer connection,
        string prefix,
        string tenantId,
        string channel)
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
            },
            configureNearCache: options =>
            {
                options.InvalidationChannel = channel;
                options.DefaultL1Ttl = TimeSpan.FromMinutes(1);
                options.PublishInvalidations = true;
                options.SubscribeInvalidations = true;
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
