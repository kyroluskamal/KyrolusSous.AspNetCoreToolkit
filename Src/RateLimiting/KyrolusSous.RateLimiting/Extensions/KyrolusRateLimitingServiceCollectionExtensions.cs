namespace Microsoft.Extensions.DependencyInjection;

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using KyrolusSous.RateLimiting;
using KyrolusSous.RateLimiting.Abstractions;


/// <summary>
/// Extension methods for configuring Kyrolus Rate Limiting and partition key policies in an <see cref="IServiceCollection"/>.
/// </summary>
public static class KyrolusRateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus Rate Limiting services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">An optional action to configure <see cref="RateLimiterOptions"/>.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddKyrolusRateLimiting(
        this IServiceCollection services,
        Action<RateLimiterOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(options =>
        {
            configureOptions?.Invoke(options);
        });

        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IKyrolusPartitionKeyResolver"/> as a singleton in the service collection.
    /// </summary>
    /// <typeparam name="TResolver">The type of the partition key resolver implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddKyrolusPartitionKeyResolver<TResolver>(this IServiceCollection services)
        where TResolver : class, IKyrolusPartitionKeyResolver
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IKyrolusPartitionKeyResolver, TResolver>();
        return services;
    }
}
