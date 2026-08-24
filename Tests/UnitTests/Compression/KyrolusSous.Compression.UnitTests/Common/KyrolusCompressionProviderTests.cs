namespace KyrolusSous.Compression.UnitTests.Common;

public class KyrolusCompressionProviderTests
{
    [Fact(DisplayName = "Register and GetCompressor should resolve all registered algorithms and default compressor")]
    public void Provider_ShouldRegisterAndResolveAllAlgorithms()
    {
        var provider = new KyrolusCompressionProvider();

        provider.Register(BrotliCompressor.Instance);
        provider.Register(ZstdCompressor.Instance);
        provider.Register(Lz4Compressor.Instance);
        provider.Register(SnappyCompressor.Instance);
        provider.Register(GzipCompressor.Instance);
        provider.Register(DeflateCompressor.Instance);

        provider.GetCompressor(CompressionAlgorithm.Brotli).ShouldBeSameAs(BrotliCompressor.Instance);
        provider.GetCompressor(CompressionAlgorithm.Zstd).ShouldBeSameAs(ZstdCompressor.Instance);
        provider.GetCompressor(CompressionAlgorithm.Lz4).ShouldBeSameAs(Lz4Compressor.Instance);
        provider.GetCompressor(CompressionAlgorithm.Snappy).ShouldBeSameAs(SnappyCompressor.Instance);
        provider.GetCompressor(CompressionAlgorithm.Gzip).ShouldBeSameAs(GzipCompressor.Instance);
        provider.GetCompressor(CompressionAlgorithm.Deflate).ShouldBeSameAs(DeflateCompressor.Instance);

        provider.DefaultCompressor.ShouldBeSameAs(BrotliCompressor.Instance);
    }

    [Fact(DisplayName = "TryGetCompressor when algorithm is not registered should return false and null")]
    public void TryGetCompressor_WhenNotRegistered_ShouldReturnFalse()
    {
        var provider = new KyrolusCompressionProvider();

        provider.TryGetCompressor(CompressionAlgorithm.Zstd, out var compressor).ShouldBeFalse();
        compressor.ShouldBeNull();
    }

    [Fact(DisplayName = "TryGetCompressor when algorithm is registered should return true and compressor instance")]
    public void TryGetCompressor_WhenRegistered_ShouldReturnTrue()
    {
        var provider = new KyrolusCompressionProvider();
        provider.Register(GzipCompressor.Instance);

        provider.TryGetCompressor(CompressionAlgorithm.Gzip, out var compressor).ShouldBeTrue();
        compressor.ShouldBeSameAs(GzipCompressor.Instance);
    }

    [Fact(DisplayName = "GetCompressor when algorithm is not registered should throw NotSupportedException")]
    public void GetCompressor_WhenNotRegistered_ShouldThrowNotSupportedException()
    {
        var provider = new KyrolusCompressionProvider();

        var ex = Should.Throw<NotSupportedException>(() =>
            provider.GetCompressor(CompressionAlgorithm.Snappy));

        ex.Message.ShouldContain("Snappy");
    }

    [Fact(DisplayName = "DefaultCompressor when registry is empty should throw InvalidOperationException")]
    public void DefaultCompressor_WhenEmpty_ShouldThrowInvalidOperationException()
    {
        var provider = new KyrolusCompressionProvider();

        Should.Throw<InvalidOperationException>(() => _ = provider.DefaultCompressor);
    }

    [Fact(DisplayName = "DefaultCompressor when Brotli is missing should return first registered compressor")]
    public void DefaultCompressor_WhenBrotliMissing_ShouldReturnFirstRegistered()
    {
        var provider = new KyrolusCompressionProvider();
        provider.Register(GzipCompressor.Instance);

        provider.DefaultCompressor.ShouldBeSameAs(GzipCompressor.Instance);
    }

    [Fact(DisplayName = "Register with null compressor instance should throw ArgumentNullException")]
    public void Register_NullCompressor_ShouldThrowArgumentNullException()
    {
        var provider = new KyrolusCompressionProvider();

        Should.Throw<ArgumentNullException>(() => provider.Register(null!));
    }
}
