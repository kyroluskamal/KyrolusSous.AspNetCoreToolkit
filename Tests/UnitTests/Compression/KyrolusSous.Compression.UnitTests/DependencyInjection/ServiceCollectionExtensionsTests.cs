namespace KyrolusSous.Compression.UnitTests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddKyrolusBrotliCompression should register BrotliCompressor, provider, and options")]
    public void AddKyrolusBrotliCompression_ShouldRegisterBrotliAndOptions()
    {
        var services = new ServiceCollection();
        services.AddKyrolusBrotliCompression(opt => opt.WithMinSizeBytes(512));

        var sp = services.BuildServiceProvider();
        var compressor = sp.GetRequiredService<IKyrolusCompressor>();
        var provider = sp.GetRequiredService<IKyrolusCompressionProvider>();
        var options = sp.GetRequiredService<IOptions<KyrolusResponseCompressionOptions>>().Value;

        compressor.ShouldBeSameAs(BrotliCompressor.Instance);
        provider.ShouldNotBeNull();
        options.PreferredAlgorithm.ShouldBe(KyrolusCompressionAlgorithm.Brotli);
        options.MinSizeBytes.ShouldBe(512);
    }

    [Fact(DisplayName = "AddKyrolusZstdCompression should register ZstdCompressor and configure preferred algorithm to Zstd")]
    public void AddKyrolusZstdCompression_ShouldRegisterZstdAndOptions()
    {
        var services = new ServiceCollection();
        services.AddKyrolusZstdCompression();

        var sp = services.BuildServiceProvider();
        var compressor = sp.GetRequiredService<IKyrolusCompressor>();
        var options = sp.GetRequiredService<IOptions<KyrolusResponseCompressionOptions>>().Value;

        compressor.ShouldBeSameAs(ZstdCompressor.Instance);
        options.PreferredAlgorithm.ShouldBe(KyrolusCompressionAlgorithm.Zstd);
    }

    [Fact(DisplayName = "AddKyrolusLz4Compression should register Lz4Compressor and configure preferred algorithm to Lz4")]
    public void AddKyrolusLz4Compression_ShouldRegisterLz4AndOptions()
    {
        var services = new ServiceCollection();
        services.AddKyrolusLz4Compression();

        var sp = services.BuildServiceProvider();
        var compressor = sp.GetRequiredService<IKyrolusCompressor>();
        var options = sp.GetRequiredService<IOptions<KyrolusResponseCompressionOptions>>().Value;

        compressor.ShouldBeSameAs(Lz4Compressor.Instance);
        options.PreferredAlgorithm.ShouldBe(KyrolusCompressionAlgorithm.Lz4);
    }

    [Fact(DisplayName = "AddKyrolusSnappyCompression should register SnappyCompressor and configure preferred algorithm to Snappy")]
    public void AddKyrolusSnappyCompression_ShouldRegisterSnappyAndOptions()
    {
        var services = new ServiceCollection();
        services.AddKyrolusSnappyCompression();

        var sp = services.BuildServiceProvider();
        var compressor = sp.GetRequiredService<IKyrolusCompressor>();
        var options = sp.GetRequiredService<IOptions<KyrolusResponseCompressionOptions>>().Value;

        compressor.ShouldBeSameAs(SnappyCompressor.Instance);
        options.PreferredAlgorithm.ShouldBe(KyrolusCompressionAlgorithm.Snappy);
    }

    [Fact(DisplayName = "AddKyrolusGzipCompression should register GzipCompressor and configure preferred algorithm to Gzip")]
    public void AddKyrolusGzipCompression_ShouldRegisterGzipAndOptions()
    {
        var services = new ServiceCollection();
        services.AddKyrolusGzipCompression();

        var sp = services.BuildServiceProvider();
        var compressor = sp.GetRequiredService<IKyrolusCompressor>();
        var options = sp.GetRequiredService<IOptions<KyrolusResponseCompressionOptions>>().Value;

        compressor.ShouldBeSameAs(GzipCompressor.Instance);
        options.PreferredAlgorithm.ShouldBe(KyrolusCompressionAlgorithm.Gzip);
    }

    [Fact(DisplayName = "AddKyrolusDeflateCompression should register DeflateCompressor and configure preferred algorithm to Deflate")]
    public void AddKyrolusDeflateCompression_ShouldRegisterDeflateAndOptions()
    {
        var services = new ServiceCollection();
        services.AddKyrolusDeflateCompression();

        var sp = services.BuildServiceProvider();
        var compressor = sp.GetRequiredService<IKyrolusCompressor>();
        var options = sp.GetRequiredService<IOptions<KyrolusResponseCompressionOptions>>().Value;

        compressor.ShouldBeSameAs(DeflateCompressor.Instance);
        options.PreferredAlgorithm.ShouldBe(KyrolusCompressionAlgorithm.Deflate);
    }
}
