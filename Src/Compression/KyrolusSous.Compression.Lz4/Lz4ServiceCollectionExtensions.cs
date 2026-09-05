namespace KyrolusSous.Compression;

public static class Lz4ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ultra-fast LZ4 compressor, provider, and response compression options in a single call.
    /// </summary>
    public static IServiceCollection AddKyrolusLz4Compression(
        this IServiceCollection services,
        Action<KyrolusResponseCompressionOptions>? configure = null)
    {
        KyrolusCompressionProvider.Instance.Register(Lz4Compressor.Instance);
        services.TryAddSingleton<IKyrolusCompressionProvider>(KyrolusCompressionProvider.Instance);
        services.TryAddSingleton<IKyrolusCompressor>(Lz4Compressor.Instance);

        services.Configure<KyrolusResponseCompressionOptions>(options =>
        {
            options.PreferredAlgorithm = KyrolusCompressionAlgorithm.Lz4;
            configure?.Invoke(options);
        });

        return services;
    }
}
