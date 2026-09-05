namespace KyrolusSous.Compression;

public static class GzipServiceCollectionExtensions
{
    /// <summary>
    /// Registers standard Gzip compressor, provider, and response compression options in a single call.
    /// </summary>
    public static IServiceCollection AddKyrolusGzipCompression(
        this IServiceCollection services,
        Action<KyrolusResponseCompressionOptions>? configure = null)
    {
        KyrolusCompressionProvider.Instance.Register(GzipCompressor.Instance);
        services.TryAddSingleton<IKyrolusCompressionProvider>(KyrolusCompressionProvider.Instance);
        services.TryAddSingleton<IKyrolusCompressor>(GzipCompressor.Instance);

        services.Configure<KyrolusResponseCompressionOptions>(options =>
        {
            options.PreferredAlgorithm = KyrolusCompressionAlgorithm.Gzip;
            configure?.Invoke(options);
        });

        return services;
    }
}
