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

}
