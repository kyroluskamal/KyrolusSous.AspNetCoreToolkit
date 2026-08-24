namespace KyrolusSous.Compression;

public static class DeflateServiceCollectionExtensions
{
    /// <summary>
    /// Registers raw Deflate compressor, provider, and response compression options in a single call.
    /// </summary>
    public static IServiceCollection AddKyrolusDeflateCompression(
        this IServiceCollection services,
        Action<KyrolusResponseCompressionOptions>? configure = null)
    {
        KyrolusCompressionProvider.Instance.Register(DeflateCompressor.Instance);
        services.TryAddSingleton<ICompressionProvider>(KyrolusCompressionProvider.Instance);
        services.TryAddSingleton<ICompressor>(DeflateCompressor.Instance);

        services.Configure<KyrolusResponseCompressionOptions>(options =>
        {
            options.PreferredAlgorithm = CompressionAlgorithm.Deflate;
            configure?.Invoke(options);
        });

        return services;
    }
}
