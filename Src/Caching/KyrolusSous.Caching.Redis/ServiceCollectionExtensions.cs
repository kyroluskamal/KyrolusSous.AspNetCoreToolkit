namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Extension methods for registering Kyrolus Redis Caching, Near-Cache, Distributed Locking, Pub/Sub, 
/// Output Caching, and Health Checks into ASP.NET Core Dependency Injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus Redis Cache Provider, Distributed Locking (<see cref="IKyrolusDistributedLockProvider"/>), and Typed Pub/Sub (<see cref="IKyrolusRedisPubSub"/>).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// The primary registration method to enable distributed Redis caching across your entire application:
    /// <code>
    /// builder.Services.AddKyrolusRedisCacheProvider(options =>
    /// {
    ///     options.ConnectionString = "localhost:6379";
    ///     options.WithBrotliCompression();
    ///     options.WithCircuitBreaker();
    /// });
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional lambda to configure <see cref="KyrolusRedisCacheOptions"/>.</param>
    /// <returns>The service collection for method chaining.</returns>
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

        services.TryAddSingleton<KyrolusRedisCacheProvider>();
        services.TryAddSingleton<IKyrolusCacheProvider>(sp => sp.GetRequiredService<KyrolusRedisCacheProvider>());

        // Standalone Distributed Lock & Typed Pub/Sub
        services.TryAddSingleton<KyrolusRedisDistributedLockProvider>();
        services.TryAddSingleton<IKyrolusDistributedLockProvider>(sp => sp.GetRequiredService<KyrolusRedisDistributedLockProvider>());
        services.TryAddSingleton<IKyrolusRedisPubSub, KyrolusRedisPubSub>();

        return services;
    }

    /// <summary>
    /// Adds Kyrolus Redis Cache with an explicit connection string.
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
    /// Adapts the Kyrolus Redis Cache to the standard Microsoft <see cref="IDistributedCache"/> interface for ASP.NET Core session and token support.
    /// </summary>
    public static IServiceCollection AddKyrolusRedisDistributedCache(this IServiceCollection services)
    {
        services.TryAddSingleton<IDistributedCache, KyrolusRedisDistributedCacheAdapter>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="KyrolusRedisOutputCacheStore"/> as ASP.NET Core's <see cref="IOutputCacheStore"/> for full HTTP response caching.
    /// </summary>
    public static IServiceCollection AddKyrolusRedisOutputCache(this IServiceCollection services)
    {
        services.TryAddSingleton<IOutputCacheStore, KyrolusRedisOutputCacheStore>();
        return services;
    }

    /// <summary>
    /// Registers a structured logging observer (<see cref="KyrolusCacheLoggingObserver"/>) to log cache events (misses, sets, removes, errors) to <see cref="ILogger{TCategoryName}"/>.
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
            var compressor = serviceProvider.GetService<IKyrolusCompressor>();
            var provider = serviceProvider.GetService<IKyrolusCompressionProvider>();

            IKyrolusCachePayloadTransformer transformer;
            if (compressor is not null)
            {
                transformer = new KyrolusCompressionCachePayloadTransformer(
                    compressor,
                    provider,
                    options.CompressionThresholdBytes,
                    options.CompressionLevel);
            }
            else if (provider is not null && provider.TryGetCompressor(options.KyrolusCompressionAlgorithm, out var resolvedComp) && resolvedComp is not null)
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
    /// Adds high-performance two-tier near cache (L1 In-Memory + L2 Redis) with automatic Pub/Sub synchronization.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (100x Faster Read Throughput):</b>
    /// Hot cache keys are returned in 50 nanoseconds from local RAM. Writes are synced across all servers via Redis Pub/Sub.
    /// </remarks>
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
        services.Replace(ServiceDescriptor.Singleton<IKyrolusCacheProvider>(sp => sp.GetRequiredService<KyrolusRedisNearCacheProvider>()));
        return services;
    }

    /// <summary>
    /// Adds two-tier near cache with an explicit connection string.
    /// </summary>
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

    /// <summary>
    /// Registers the standalone Redis cache invalidation bus (<see cref="IKyrolusCacheInvalidationBus"/>) in DI.
    /// </summary>
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

    /// <summary>
    /// Registers a health check probe for Redis cache connectivity and PING roundtrip latency into ASP.NET Core Health Checks.
    /// </summary>
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
