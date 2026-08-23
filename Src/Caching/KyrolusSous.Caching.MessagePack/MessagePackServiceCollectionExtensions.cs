using KyrolusSous.Caching.Abstractions;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Caching.MessagePack;

public static class MessagePackServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="KyrolusMessagePackCacheSerializer"/> as the <see cref="IKyrolusCacheSerializer"/> in DI.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional MessagePack serializer options.</param>
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
    /// Registers <see cref="KyrolusMessagePackCacheSerializer"/> with LZ4 compression enabled.
    /// </summary>
    public static IServiceCollection AddKyrolusMessagePackSerializerWithLz4(this IServiceCollection services)
    {
        var serializer = KyrolusMessagePackCacheSerializer.CreateWithLz4Compression();
        services.Replace(ServiceDescriptor.Singleton<IKyrolusCacheSerializer>(serializer));
        return services;
    }
}
