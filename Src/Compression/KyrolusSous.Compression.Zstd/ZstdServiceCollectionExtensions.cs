namespace KyrolusSous.Compression;

public static class ZstdServiceCollectionExtensions
{
    /// <summary>
    /// Registers Meta's Zstandard (Zstd) compressor, provider, and response compression options in a single call.
    /// </summary>
    public static IServiceCollection AddKyrolusZstdCompression(
        this IServiceCollection services,
        Action<KyrolusResponseCompressionOptions>? configure = null)
    {
        KyrolusCompressionProvider.Instance.Register(ZstdCompressor.Instance);
        services.TryAddSingleton<IKyrolusCompressionProvider>(KyrolusCompressionProvider.Instance);
        services.TryAddSingleton<IKyrolusCompressor>(ZstdCompressor.Instance);

        services.Configure<KyrolusResponseCompressionOptions>(options =>
        {
            options.PreferredAlgorithm = KyrolusCompressionAlgorithm.Zstd;
            configure?.Invoke(options);
        });

        return services;
    }
}
