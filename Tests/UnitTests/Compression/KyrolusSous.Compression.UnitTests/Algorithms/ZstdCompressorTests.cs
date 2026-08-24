namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class ZstdCompressorTests : CompressorTestBase
{
    protected override ICompressor Compressor => ZstdCompressor.Instance;
    protected override CompressionAlgorithm ExpectedAlgorithm => CompressionAlgorithm.Zstd;

    [Fact(DisplayName = "ZstdCompressor Instance singleton should not be null and have Zstd algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        ZstdCompressor.Instance.ShouldNotBeNull();
        ZstdCompressor.Instance.Algorithm.ShouldBe(CompressionAlgorithm.Zstd);
    }

    [Theory(DisplayName = "ZstdCompressor with different compression levels should compress and decompress correctly")]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    [InlineData(CompressionLevel.NoCompression)]
    [InlineData((CompressionLevel)99)]
    public async Task Zstd_DifferentCompressionLevels_ShouldRoundtrip(CompressionLevel level)
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using var src = new MemoryStream(data);
        using var dst = new MemoryStream();

        await ZstdCompressor.Instance.CompressAsync(src, dst, level);
        dst.Position = 0;

        using var decomp = new MemoryStream();
        await ZstdCompressor.Instance.DecompressAsync(dst, decomp);

        decomp.ToArray().ShouldBe(data);
    }
}
