namespace Microsoft.AspNetCore.RateLimiting;

using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using KyrolusSous.RateLimiting;
using KyrolusSous.RateLimiting.Abstractions;

/// <summary>
/// Fluent extension methods for configuring partitioned rate limiting policies on <see cref="RateLimiterOptions"/>.
/// </summary>
public static class KyrolusRateLimiterOptionsExtensions
{
    /// <summary>
    /// Adds a partitioned sliding window rate limiter policy using a partition key resolver delegate.
    /// Automatically honors <see cref="IKyrolusRateLimitMetadata.IsExempt"/> on matching endpoints.
    /// </summary>
    /// <param name="options">The rate limiter options.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="partitionKeyResolver">A function resolving the partition key string from the active <see cref="HttpContext"/>.</param>
    /// <param name="configure">An action to configure <see cref="SlidingWindowRateLimiterOptions"/>.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> for chaining.</returns>
    public static RateLimiterOptions AddKyrolusSlidingWindowPolicy(
        this RateLimiterOptions options,
        string policyName,
        Func<HttpContext, string> partitionKeyResolver,
        Action<SlidingWindowRateLimiterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(partitionKeyResolver);
        ArgumentNullException.ThrowIfNull(configure);

        return options.AddPolicy(policyName, context =>
        {
            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IKyrolusRateLimitMetadata>();
            if (metadata?.IsExempt == true)
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }

            var key = partitionKeyResolver(context);
            return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
            {
                var limiterOptions = new SlidingWindowRateLimiterOptions();
                configure(limiterOptions);
                return limiterOptions;
            });
        });
    }

    /// <summary>
    /// Adds a partitioned sliding window rate limiter policy with a dynamic factory that receives the resolved partition key.
    /// </summary>
    /// <param name="options">The rate limiter options.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="partitionKeyResolver">A function resolving the partition key string from the active <see cref="HttpContext"/>.</param>
    /// <param name="configure">An action to configure <see cref="SlidingWindowRateLimiterOptions"/> with the partition key.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> for chaining.</returns>
    public static RateLimiterOptions AddKyrolusSlidingWindowPolicy(
        this RateLimiterOptions options,
        string policyName,
        Func<HttpContext, string> partitionKeyResolver,
        Action<string, SlidingWindowRateLimiterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(partitionKeyResolver);
        ArgumentNullException.ThrowIfNull(configure);

        return options.AddPolicy(policyName, context =>
        {
            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IKyrolusRateLimitMetadata>();
            if (metadata?.IsExempt == true)
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }

            var key = partitionKeyResolver(context);
            return RateLimitPartition.GetSlidingWindowLimiter(key, partitionKey =>
            {
                var limiterOptions = new SlidingWindowRateLimiterOptions();
                configure(partitionKey, limiterOptions);
                return limiterOptions;
            });
        });
    }

    /// <summary>
    /// Adds a partitioned sliding window rate limiter policy using a registered <see cref="IKyrolusPartitionKeyResolver"/> from DI.
    /// </summary>
    /// <typeparam name="TResolver">The registered partition key resolver type.</typeparam>
    /// <param name="options">The rate limiter options.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="configure">An action to configure <see cref="SlidingWindowRateLimiterOptions"/>.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> for chaining.</returns>
    public static RateLimiterOptions AddKyrolusSlidingWindowPolicy<TResolver>(
        this RateLimiterOptions options,
        string policyName,
        Action<SlidingWindowRateLimiterOptions> configure)
        where TResolver : IKyrolusPartitionKeyResolver
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(configure);

        return options.AddPolicy(policyName, context =>
        {
            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IKyrolusRateLimitMetadata>();
            if (metadata?.IsExempt == true)
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }

            var resolver = context.RequestServices.GetService<TResolver>()
                ?? context.RequestServices.GetServices<IKyrolusPartitionKeyResolver>().OfType<TResolver>().FirstOrDefault()
                ?? ActivatorUtilities.CreateInstance<TResolver>(context.RequestServices);
            var key = resolver.ResolveKey(context);
            return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
            {
                var limiterOptions = new SlidingWindowRateLimiterOptions();
                configure(limiterOptions);
                return limiterOptions;
            });
        });
    }

    /// <summary>
    /// Adds a partitioned fixed window rate limiter policy using a partition key resolver delegate.
    /// Automatically honors <see cref="IKyrolusRateLimitMetadata.IsExempt"/> on matching endpoints.
    /// </summary>
    /// <param name="options">The rate limiter options.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="partitionKeyResolver">A function resolving the partition key string from the active <see cref="HttpContext"/>.</param>
    /// <param name="configure">An action to configure <see cref="FixedWindowRateLimiterOptions"/>.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> for chaining.</returns>
    public static RateLimiterOptions AddKyrolusFixedWindowPolicy(
        this RateLimiterOptions options,
        string policyName,
        Func<HttpContext, string> partitionKeyResolver,
        Action<FixedWindowRateLimiterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(partitionKeyResolver);
        ArgumentNullException.ThrowIfNull(configure);

        return options.AddPolicy(policyName, context =>
        {
            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IKyrolusRateLimitMetadata>();
            if (metadata?.IsExempt == true)
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }

            var key = partitionKeyResolver(context);
            return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            {
                var limiterOptions = new FixedWindowRateLimiterOptions();
                configure(limiterOptions);
                return limiterOptions;
            });
        });
    }

    /// <summary>
    /// Adds a partitioned fixed window rate limiter policy with a dynamic factory that receives the resolved partition key.
    /// </summary>
    /// <param name="options">The rate limiter options.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="partitionKeyResolver">A function resolving the partition key string from the active <see cref="HttpContext"/>.</param>
    /// <param name="configure">An action to configure <see cref="FixedWindowRateLimiterOptions"/> with the partition key.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> for chaining.</returns>
    public static RateLimiterOptions AddKyrolusFixedWindowPolicy(
        this RateLimiterOptions options,
        string policyName,
        Func<HttpContext, string> partitionKeyResolver,
        Action<string, FixedWindowRateLimiterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(partitionKeyResolver);
        ArgumentNullException.ThrowIfNull(configure);

        return options.AddPolicy(policyName, context =>
        {
            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IKyrolusRateLimitMetadata>();
            if (metadata?.IsExempt == true)
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }

            var key = partitionKeyResolver(context);
            return RateLimitPartition.GetFixedWindowLimiter(key, partitionKey =>
            {
                var limiterOptions = new FixedWindowRateLimiterOptions();
                configure(partitionKey, limiterOptions);
                return limiterOptions;
            });
        });
    }

    /// <summary>
    /// Adds a partitioned token bucket rate limiter policy using a partition key resolver delegate.
    /// Automatically honors <see cref="IKyrolusRateLimitMetadata.IsExempt"/> on matching endpoints.
    /// </summary>
    /// <param name="options">The rate limiter options.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="partitionKeyResolver">A function resolving the partition key string from the active <see cref="HttpContext"/>.</param>
    /// <param name="configure">An action to configure <see cref="TokenBucketRateLimiterOptions"/>.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> for chaining.</returns>
    public static RateLimiterOptions AddKyrolusTokenBucketPolicy(
        this RateLimiterOptions options,
        string policyName,
        Func<HttpContext, string> partitionKeyResolver,
        Action<TokenBucketRateLimiterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(partitionKeyResolver);
        ArgumentNullException.ThrowIfNull(configure);

        return options.AddPolicy(policyName, context =>
        {
            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IKyrolusRateLimitMetadata>();
            if (metadata?.IsExempt == true)
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }

            var key = partitionKeyResolver(context);
            return RateLimitPartition.GetTokenBucketLimiter(key, _ =>
            {
                var limiterOptions = new TokenBucketRateLimiterOptions();
                configure(limiterOptions);
                return limiterOptions;
            });
        });
    }

    /// <summary>
    /// Adds a partitioned concurrency rate limiter policy using a partition key resolver delegate.
    /// Automatically honors <see cref="IKyrolusRateLimitMetadata.IsExempt"/> on matching endpoints.
    /// </summary>
    /// <param name="options">The rate limiter options.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="partitionKeyResolver">A function resolving the partition key string from the active <see cref="HttpContext"/>.</param>
    /// <param name="configure">An action to configure <see cref="ConcurrencyLimiterOptions"/>.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> for chaining.</returns>
    public static RateLimiterOptions AddKyrolusConcurrencyPolicy(
        this RateLimiterOptions options,
        string policyName,
        Func<HttpContext, string> partitionKeyResolver,
        Action<ConcurrencyLimiterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(partitionKeyResolver);
        ArgumentNullException.ThrowIfNull(configure);

        return options.AddPolicy(policyName, context =>
        {
            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IKyrolusRateLimitMetadata>();
            if (metadata?.IsExempt == true)
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }

            var key = partitionKeyResolver(context);
            return RateLimitPartition.GetConcurrencyLimiter(key, _ =>
            {
                var limiterOptions = new ConcurrencyLimiterOptions();
                configure(limiterOptions);
                return limiterOptions;
            });
        });
    }
}
