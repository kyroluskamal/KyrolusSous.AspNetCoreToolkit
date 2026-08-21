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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
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
