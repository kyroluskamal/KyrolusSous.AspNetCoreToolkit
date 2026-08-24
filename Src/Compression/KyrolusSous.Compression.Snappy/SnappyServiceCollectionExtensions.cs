namespace KyrolusSous.Compression;

public static class SnappyServiceCollectionExtensions
{
    /// <summary>
    /// Registers Google's Snappy compressor, provider, and response compression options in a single call.
    /// </summary>
    public static IServiceCollection AddKyrolusSnappyCompression(
        this IServiceCollection services,
        Action<KyrolusResponseCompressionOptions>? configure = null)
    {
        KyrolusCompressionProvider.Instance.Register(SnappyCompressor.Instance);
        services.TryAddSingleton<ICompressionProvider>(KyrolusCompressionProvider.Instance);
        services.TryAddSingleton<ICompressor>(SnappyCompressor.Instance);

        services.Configure<KyrolusResponseCompressionOptions>(options =>
        {
            options.PreferredAlgorithm = CompressionAlgorithm.Snappy;
            configure?.Invoke(options);
        });

        return services;
    }
}
