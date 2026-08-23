using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Compression;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus Compression core services to the service collection (Brotli, Zstd, LZ4, Snappy, Gzip, Deflate).
    /// </summary>
    public static IServiceCollection AddKyrolusCompression(this IServiceCollection services)
    {
        services.TryAddSingleton<ICompressionProvider>(KyrolusCompressionProvider.Instance);
        services.TryAddSingleton<BrotliCompressor>(BrotliCompressor.Instance);
        services.TryAddSingleton<ZstdCompressor>(ZstdCompressor.Instance);
        services.TryAddSingleton<Lz4Compressor>(Lz4Compressor.Instance);
        services.TryAddSingleton<SnappyCompressor>(SnappyCompressor.Instance);
        services.TryAddSingleton<GzipCompressor>(GzipCompressor.Instance);
        services.TryAddSingleton<DeflateCompressor>(DeflateCompressor.Instance);
        services.TryAddSingleton<ICompressor>(sp => sp.GetRequiredService<ICompressionProvider>().DefaultCompressor);
        return services;
    }

    /// <summary>
    /// Adds automatic HTTP response compression services with configurable MIME types and route filters.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    public static IServiceCollection AddKyrolusResponseCompression(
        this IServiceCollection services,
        Action<KyrolusResponseCompressionOptions>? configure = null)
    {
        services.AddKyrolusCompression();

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<KyrolusResponseCompressionOptions>();
        }

        return services;
    }
}
