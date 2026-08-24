namespace KyrolusSous.Compression;

public static class BrotliServiceCollectionExtensions
{
    /// <summary>
    /// Registers Google's Brotli compressor, provider, and response compression options in a single call.
    /// </summary>
    public static IServiceCollection AddKyrolusBrotliCompression(
        this IServiceCollection services,
        Action<KyrolusResponseCompressionOptions>? configure = null)
    {
        KyrolusCompressionProvider.Instance.Register(BrotliCompressor.Instance);
        services.TryAddSingleton<ICompressionProvider>(KyrolusCompressionProvider.Instance);
        services.TryAddSingleton<ICompressor>(BrotliCompressor.Instance);

        services.Configure<KyrolusResponseCompressionOptions>(options =>
        {
            options.PreferredAlgorithm = CompressionAlgorithm.Brotli;
            configure?.Invoke(options);
        });

        return services;
    }
}
