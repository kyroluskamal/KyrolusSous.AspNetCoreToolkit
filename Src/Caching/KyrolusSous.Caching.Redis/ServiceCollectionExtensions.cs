using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Compression;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus Redis Cache Provider, Distributed Locking (<see cref="IDistributedLockProvider"/>), and Typed Pub/Sub (<see cref="IKyrolusRedisPubSub"/>).
    /// </summary>
    public static IServiceCollection AddKyrolusRedisCacheProvider(
        this IServiceCollection services,
        Action<KyrolusRedisCacheOptions>? configure = null)
    {
        services.TryAddSingleton(sp =>
        {
            var options = new KyrolusRedisCacheOptions();
            configure?.Invoke(options);
            KyrolusRedisCacheOptionsValidator.Validate(options);
            return options;
        });

        // Ensure IConnectionMultiplexer is resolved: if ConnectionString is provided in options, register it
        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<KyrolusRedisCacheOptions>();
            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return ConnectionMultiplexer.Connect(options.ConnectionString);
            }

            throw new InvalidOperationException(
                "No IConnectionMultiplexer was registered, and no ConnectionString was configured in KyrolusRedisCacheOptions.");
        });

        services.TryAddSingleton<IKyrolusCacheSerializer>(CreateCacheSerializer);
        services.TryAddSingleton<IKyrolusCacheObserver>(KyrolusNullCacheObserver.Instance);
        services.TryAddSingleton<KyrolusCachePolicyRegistry>();
        services.TryAddSingleton<IKyrolusCachePolicyProvider>(sp => sp.GetRequiredService<KyrolusCachePolicyRegistry>());
        services.TryAddSingleton<IKyrolusCacheKeyFactory>(sp =>
        {
            var options = sp.GetRequiredService<KyrolusRedisCacheOptions>();
            return new KyrolusCacheKeyFactory(options.KeyPrefix);
        });

        services.TryAddSingleton(sp =>
            new KyrolusRedisCacheDependencies(
                sp.GetRequiredService<IKyrolusCacheSerializer>(),
                sp.GetRequiredService<IKyrolusCacheKeyFactory>(),
                sp.GetRequiredService<KyrolusRedisCacheOptions>(),
                sp.GetRequiredService<IKyrolusCacheObserver>(),
                sp.GetRequiredService<IKyrolusCachePolicyProvider>()));

        services.TryAddSingleton<RedisCacheProvider>();
        services.TryAddSingleton<ICacheProvider>(sp => sp.GetRequiredService<RedisCacheProvider>());

        // Standalone Distributed Lock & Typed Pub/Sub
        services.TryAddSingleton<IDistributedLockProvider, RedisDistributedLockProvider>();
        services.TryAddSingleton<IKyrolusRedisPubSub, KyrolusRedisPubSub>();

        return services;
    }

    /// <summary>
    /// Adds Kyrolus Redis Cache with connection string.
    /// </summary>
    public static IServiceCollection AddKyrolusRedisCacheProvider(
        this IServiceCollection services,
        string connectionString,
        Action<KyrolusRedisCacheOptions>? configure = null)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        return services.AddKyrolusRedisCacheProvider(opts =>
        {
            opts.ConnectionString = connectionString;
            configure?.Invoke(opts);
        });
    }

    /// <summary>
    /// Shortcut alias for <see cref="AddKyrolusRedisCacheProvider(IServiceCollection, Action{KyrolusRedisCacheOptions}?)"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusRedisCache(
        this IServiceCollection services,
        Action<KyrolusRedisCacheOptions>? configure = null) =>
        services.AddKyrolusRedisCacheProvider(configure);

    /// <summary>
    /// Shortcut alias for <see cref="AddKyrolusRedisCacheProvider(IServiceCollection, string, Action{KyrolusRedisCacheOptions}?)"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusRedisCache(
        this IServiceCollection services,
        string connectionString,
        Action<KyrolusRedisCacheOptions>? configure = null) =>
        services.AddKyrolusRedisCacheProvider(connectionString, configure);

    /// <summary>
    /// Adds Kyrolus Redis Cache by automatically binding from <see cref="IConfiguration"/> (e.g. from appsettings.json section "Redis" or "ConnectionStrings:Redis").
    /// </summary>
    public static IServiceCollection AddKyrolusRedisCache(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Redis",
        Action<KyrolusRedisCacheOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddKyrolusRedisCacheProvider(options =>
        {
            var section = configuration.GetSection(sectionName);
            if (section.Exists())
            {
                section.Bind(options);
            }

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                options.ConnectionString = configuration.GetConnectionString("Redis")
                    ?? configuration.GetConnectionString(sectionName);
            }

            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Registers <see cref="KyrolusRedisDistributedCacheAdapter"/> as ASP.NET Core <see cref="IDistributedCache"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusRedisDistributedCache(this IServiceCollection services)
    {
        services.TryAddSingleton<IDistributedCache, KyrolusRedisDistributedCacheAdapter>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="KyrolusRedisOutputCacheStore"/> as ASP.NET Core <see cref="IOutputCacheStore"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusRedisOutputCache(this IServiceCollection services)
    {
        services.TryAddSingleton<IOutputCacheStore, KyrolusRedisOutputCacheStore>();
        return services;
    }

    /// <summary>
    /// Adds logging observer for cache events.
    /// </summary>
    public static IServiceCollection AddKyrolusCacheLoggingObserver(
        this IServiceCollection services,
        Action<KyrolusCacheLoggingObserverOptions>? configure = null)
    {
        services.TryAddSingleton(sp =>
        {
            var options = new KyrolusCacheLoggingObserverOptions();
            configure?.Invoke(options);
            return options;
        });

        services.AddSingleton<IKyrolusCacheObserver, KyrolusCacheLoggingObserver>();
        return services;
    }

    private static IKyrolusCacheSerializer CreateCacheSerializer(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<KyrolusRedisCacheOptions>();
        var baseSerializer = new KyrolusJsonCacheSerializer();
        var transformers = new List<IKyrolusCachePayloadTransformer>();

        var registered = serviceProvider.GetServices<IKyrolusCachePayloadTransformer>();
        foreach (var transformer in registered)
        {
            transformers.Add(WrapOrdered(transformer, 0));
        }

        if (options.EnableCompression)
        {
            var compressor = serviceProvider.GetService<ICompressor>();
            var provider = serviceProvider.GetService<ICompressionProvider>();

            IKyrolusCachePayloadTransformer transformer;
            if (compressor is not null)
            {
                transformer = new KyrolusCompressionCachePayloadTransformer(
                    compressor,
                    provider,
                    options.CompressionThresholdBytes,
                    options.CompressionLevel);
            }
            else if (provider is not null && provider.TryGetCompressor(options.CompressionAlgorithm, out var resolvedComp) && resolvedComp is not null)
            {
                transformer = new KyrolusCompressionCachePayloadTransformer(
                    resolvedComp,
                    provider,
                    options.CompressionThresholdBytes,
                    options.CompressionLevel);
            }
            else
            {
                // Fallback to pure built-in Brotli transformer
                transformer = new KyrolusBrotliCachePayloadTransformer(
                    options.CompressionThresholdBytes,
                    options.CompressionLevel);
            }

            transformers.Add(WrapOrdered(transformer, options.CompressionOrder));
        }

        if (options.EnableEncryption)
        {
            var key = ResolveEncryptionKey(options);
            var iv = ResolveEncryptionIv(options);
            transformers.Add(WrapOrdered(new KyrolusAesCachePayloadTransformer(key, iv), options.EncryptionOrder));
        }

        var ordered = transformers
            .Select(transformer => WrapOrdered(transformer, 0))
            .OrderBy(transformer => transformer.Order)
            .Cast<IKyrolusCachePayloadTransformer>()
            .ToArray();

        return ordered.Length == 0
            ? baseSerializer
            : new KyrolusTransformingCacheSerializer(baseSerializer, ordered);
    }

    private static byte[] ResolveEncryptionKey(KyrolusRedisCacheOptions options)
    {
        if (options.EncryptionKey is { Length: > 0 })
        {
            return options.EncryptionKey;
        }

        if (!string.IsNullOrWhiteSpace(options.EncryptionKeyBase64))
        {
            return Convert.FromBase64String(options.EncryptionKeyBase64);
        }

        throw new InvalidOperationException("Redis cache encryption is enabled but no key is configured.");
    }

    private static byte[]? ResolveEncryptionIv(KyrolusRedisCacheOptions options)
    {
        if (options.EncryptionIv is { Length: > 0 })
        {
            return options.EncryptionIv;
        }

        if (!string.IsNullOrWhiteSpace(options.EncryptionIvBase64))
        {
            return Convert.FromBase64String(options.EncryptionIvBase64);
        }

        return null;
    }

    private static IKyrolusOrderedCachePayloadTransformer WrapOrdered(IKyrolusCachePayloadTransformer transformer, int defaultOrder)
    {
        return transformer is IKyrolusOrderedCachePayloadTransformer ordered
            ? ordered
            : new KyrolusOrderedCachePayloadTransformer(transformer, defaultOrder);
    }

    /// <summary>
    /// Adds two-tier near cache (L1 In-Memory + L2 Redis) with automatic invalidation via Pub/Sub.
    /// </summary>
    public static IServiceCollection AddKyrolusRedisNearCache(
        this IServiceCollection services,
        Action<KyrolusRedisCacheOptions>? configure = null,
        Action<KyrolusRedisNearCacheOptions>? configureNearCache = null)
    {
        services.AddMemoryCache();
        services.AddKyrolusRedisCacheProvider(configure);

        services.TryAddSingleton(sp =>
        {
            var options = new KyrolusRedisNearCacheOptions();
            configureNearCache?.Invoke(options);
            KyrolusRedisCacheOptionsValidator.Validate(options);
            return options;
        });

        services.TryAddSingleton(sp =>
        {
            var nearCacheOptions = sp.GetRequiredService<KyrolusRedisNearCacheOptions>();
            return KyrolusRedisInvalidationOptions.FromNearCacheOptions(nearCacheOptions);
        });

        services.TryAddSingleton<IKyrolusCacheInvalidationBus>(sp =>
            new KyrolusRedisInvalidationBus(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<KyrolusRedisInvalidationOptions>()));

        services.TryAddSingleton<KyrolusRedisNearCacheProvider>();
        services.Replace(ServiceDescriptor.Singleton<ICacheProvider>(sp => sp.GetRequiredService<KyrolusRedisNearCacheProvider>()));
        return services;
    }

    public static IServiceCollection AddKyrolusRedisNearCache(
        this IServiceCollection services,
        string connectionString,
        Action<KyrolusRedisCacheOptions>? configure = null,
        Action<KyrolusRedisNearCacheOptions>? configureNearCache = null)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        return services.AddKyrolusRedisNearCache(opts =>
        {
            opts.ConnectionString = connectionString;
            configure?.Invoke(opts);
        }, configureNearCache);
    }

    public static IServiceCollection AddKyrolusRedisInvalidationBus(
        this IServiceCollection services,
        Action<KyrolusRedisInvalidationOptions>? configure = null)
    {
        services.TryAddSingleton(sp =>
        {
            var options = new KyrolusRedisInvalidationOptions();
            configure?.Invoke(options);
            KyrolusRedisCacheOptionsValidator.Validate(options);
            return options;
        });

        services.TryAddSingleton<IKyrolusCacheInvalidationBus>(sp =>
            new KyrolusRedisInvalidationBus(
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<KyrolusRedisInvalidationOptions>()));
        return services;
    }

    public static IHealthChecksBuilder AddKyrolusRedisCacheHealthChecks(
        this IHealthChecksBuilder builder,
        Action<KyrolusRedisCacheHealthCheckOptions>? configure = null,
        string name = "kyrolus.redis.cache",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        builder.Services.TryAddSingleton(sp =>
        {
            var options = new KyrolusRedisCacheHealthCheckOptions();
            configure?.Invoke(options);
            return options;
        });

        return builder.AddCheck<KyrolusRedisCacheHealthCheck>(
            name,
            failureStatus ?? HealthStatus.Unhealthy,
            tags,
            timeout);
    }
}
