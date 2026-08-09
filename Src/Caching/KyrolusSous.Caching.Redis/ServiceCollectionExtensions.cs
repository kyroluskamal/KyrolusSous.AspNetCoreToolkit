using KyrolusSous.Caching.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

public static class ServiceCollectionExtensions
{
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
        return services;
    }

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
    public static IServiceCollection AddKyrolusRedisCacheProvider(
            this IServiceCollection services,
            string connectionString,
            Action<KyrolusRedisCacheOptions>? configure = null)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        return services.AddKyrolusRedisCacheProvider(configure);
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
            transformers.Add(WrapOrdered(
                new KyrolusGzipCachePayloadTransformer(
                    options.CompressionThresholdBytes,
                    options.CompressionLevel),
                options.CompressionOrder));
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
        return services.AddKyrolusRedisNearCache(configure, configureNearCache);
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
