namespace KyrolusSous.RateLimiting.UnitTests;

using System;
using System.Collections;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using KyrolusSous.RateLimiting;
using KyrolusSous.RateLimiting.Abstractions;

public sealed class KyrolusRateLimiterOptionsExtensionsTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    private sealed record TestPartitionResult(string Key, RateLimiter Limiter);

    private static TestPartitionResult GetPartition(RateLimiterOptions options, string policyName, HttpContext context)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var policyMapField = typeof(RateLimiterOptions).GetField("<PolicyMap>k__BackingField", flags);
        if (policyMapField?.GetValue(options) is IDictionary dict && dict.Contains(policyName))
        {
            var policy = dict[policyName];
            var method = policy?.GetType().GetMethod("GetPartition");
            var partitionObj = method?.Invoke(policy, [context]);
            var keyVal = partitionObj?.GetType().GetProperty("PartitionKey")?.GetValue(partitionObj);
            var stringKey = keyVal?.GetType().GetProperty("Key")?.GetValue(keyVal)?.ToString() ?? string.Empty;

            var factoryDelegate = (Delegate?)partitionObj?.GetType().GetProperty("Factory")?.GetValue(partitionObj);
            var limiter = (RateLimiter?)factoryDelegate?.DynamicInvoke(keyVal);

            return new TestPartitionResult(stringKey, limiter!);
        }
        throw new InvalidOperationException($"Policy '{policyName}' was not found in RateLimiterOptions.");
    }

    [Fact(DisplayName = "AddKyrolusSlidingWindowPolicy: successfully creates partitioned sliding window limiter")]
    public async Task AddKyrolusSlidingWindowPolicy_CreatesPartitionedLimiter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusRateLimiting(options =>
        {
            options.AddKyrolusSlidingWindowPolicy("sliding-ip", 
                KyrolusRateLimitPartitionKeys.GetClientIpKey, 
                opt =>
                {
                    opt.PermitLimit = 2;
                    opt.Window = TimeSpan.FromSeconds(10);
                    opt.SegmentsPerWindow = 2;
                    opt.QueueLimit = 0;
                });
        });

        var sp = services.BuildServiceProvider();
        var rateLimiterOptions = sp.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;
        context.RequestServices = sp;

        var result = GetPartition(rateLimiterOptions, "sliding-ip", context);
        result.Key.ShouldBe("198.51.100.42");

        // First 2 requests should be leased successfully
        using (var lease1 = await result.Limiter.AcquireAsync(1))
        {
            lease1.IsAcquired.ShouldBeTrue();
        }
        using (var lease2 = await result.Limiter.AcquireAsync(1))
        {
            lease2.IsAcquired.ShouldBeTrue();
        }
        // 3rd request should be rejected
        using (var lease3 = await result.Limiter.AcquireAsync(1))
        {
            lease3.IsAcquired.ShouldBeFalse();
        }
    }

    [Fact(DisplayName = "AddKyrolusSlidingWindowPolicy: honors IsExempt metadata")]
    public async Task AddKyrolusSlidingWindowPolicy_HonorsIsExemptMetadata()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusRateLimiting(options =>
        {
            options.AddKyrolusSlidingWindowPolicy("sliding-exempt",
                KyrolusRateLimitPartitionKeys.GetClientIpKey,
                opt =>
                {
                    opt.PermitLimit = 1;
                    opt.Window = TimeSpan.FromSeconds(10);
                    opt.SegmentsPerWindow = 2;
                });
        });

        var sp = services.BuildServiceProvider();
        var rateLimiterOptions = sp.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;
        context.RequestServices = sp;

        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new KyrolusRateLimitMetadata(IsExempt: true)),
            "ExemptEndpoint");
        context.SetEndpoint(endpoint);

        var result = GetPartition(rateLimiterOptions, "sliding-exempt", context);

        // Should return no-limiter (always acquired)
        for (var i = 0; i < 5; i++)
        {
            using var lease = await result.Limiter.AcquireAsync(1);
            lease.IsAcquired.ShouldBeTrue();
        }
    }

    [Fact(DisplayName = "AddKyrolusFixedWindowPolicy and TokenBucket and Concurrency policies work")]
    public async Task OtherPolicies_WorkCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusRateLimiting(options =>
        {
            options.AddKyrolusFixedWindowPolicy("fixed-ip",
                KyrolusRateLimitPartitionKeys.GetClientIpKey,
                opt =>
                {
                    opt.PermitLimit = 5;
                    opt.Window = TimeSpan.FromSeconds(10);
                });

            options.AddKyrolusTokenBucketPolicy("token-ip",
                KyrolusRateLimitPartitionKeys.GetClientIpKey,
                opt =>
                {
                    opt.TokenLimit = 10;
                    opt.TokensPerPeriod = 2;
                    opt.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
                });

            options.AddKyrolusConcurrencyPolicy("concurrency-ip",
                KyrolusRateLimitPartitionKeys.GetClientIpKey,
                opt =>
                {
                    opt.PermitLimit = 3;
                });
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        var fixedResult = GetPartition(options, "fixed-ip", context);
        fixedResult.Key.ShouldBe("198.51.100.42");
        using var lease = await fixedResult.Limiter.AcquireAsync(1);
        lease.IsAcquired.ShouldBeTrue();

        var tokenResult = GetPartition(options, "token-ip", context);
        tokenResult.Key.ShouldBe("198.51.100.42");

        var concurrencyResult = GetPartition(options, "concurrency-ip", context);
        concurrencyResult.Key.ShouldBe("198.51.100.42");
    }

    [Fact(DisplayName = "AddKyrolusSlidingWindowPolicy with DI resolver resolves key from registered service")]
    public void AddKyrolusSlidingWindowPolicy_WithDiResolver_ResolvesKey()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusPartitionKeyResolver<TestCustomKeyResolver>();
        services.AddKyrolusRateLimiting(options =>
        {
            options.AddKyrolusSlidingWindowPolicy<TestCustomKeyResolver>("sliding-di",
                opt =>
                {
                    opt.PermitLimit = 10;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.SegmentsPerWindow = 4;
                });
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext();
        context.RequestServices = sp;
        context.Request.Headers["X-Custom-Tenant"] = "tenant-enterprise-42";

        var result = GetPartition(options, "sliding-di", context);
        result.Key.ShouldBe("custom_tenant-enterprise-42");
    }

    private sealed class TestCustomKeyResolver : IKyrolusPartitionKeyResolver
    {
        public string ResolveKey(HttpContext context)
        {
            return $"custom_{context.Request.Headers["X-Custom-Tenant"]}";
        }
    }
}
