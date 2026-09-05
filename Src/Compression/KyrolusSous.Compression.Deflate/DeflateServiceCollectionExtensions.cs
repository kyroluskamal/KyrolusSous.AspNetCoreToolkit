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
        services.TryAddSingleton<IKyrolusCompressionProvider>(KyrolusCompressionProvider.Instance);
        services.TryAddSingleton<IKyrolusCompressor>(DeflateCompressor.Instance);

        services.Configure<KyrolusResponseCompressionOptions>(options =>
        {
            options.PreferredAlgorithm = KyrolusCompressionAlgorithm.Deflate;
            configure?.Invoke(options);
        });

        return services;
    }
}
