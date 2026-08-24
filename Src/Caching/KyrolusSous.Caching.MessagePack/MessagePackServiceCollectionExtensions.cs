namespace KyrolusSous.Caching.MessagePack;

/// <summary>
/// Provides extension methods for registering MessagePack cache serialization services into the dependency injection container.
/// </summary>
public static class MessagePackServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="KyrolusMessagePackCacheSerializer"/> as the application-wide <see cref="IKyrolusCacheSerializer"/> in DI,
    /// replacing the default JSON serializer.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// When you want to speed up Redis serialization by up to 5x and reduce payload bandwidth across your entire microservice fleet.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional custom MessagePack serializer options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddKyrolusMessagePackSerializer(
        this IServiceCollection services,
        MessagePackSerializerOptions? options = null)
    {
        var serializer = options is not null
            ? new KyrolusMessagePackCacheSerializer(options)
            : new KyrolusMessagePackCacheSerializer();

        services.Replace(ServiceDescriptor.Singleton<IKyrolusCacheSerializer>(serializer));
        return services;
    }

    /// <summary>
    /// Registers <see cref="KyrolusMessagePackCacheSerializer"/> with LZ4 binary block compression enabled by default.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// When caching large datasets (e.g., thousands of catalog items, customer invoices, or analytics tables) 
    /// where you want maximum size reduction combined with high-speed binary decompression.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddKyrolusMessagePackSerializerWithLz4(this IServiceCollection services)
    {
        var serializer = KyrolusMessagePackCacheSerializer.CreateWithLz4Compression();
        services.Replace(ServiceDescriptor.Singleton<IKyrolusCacheSerializer>(serializer));
        return services;
    }
}
