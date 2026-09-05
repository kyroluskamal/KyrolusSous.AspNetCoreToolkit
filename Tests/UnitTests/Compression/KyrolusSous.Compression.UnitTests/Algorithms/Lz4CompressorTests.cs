namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class Lz4CompressorTests : CompressorTestBase
{
    protected override IKyrolusCompressor Compressor => Lz4Compressor.Instance;
    protected override KyrolusCompressionAlgorithm ExpectedAlgorithm => KyrolusCompressionAlgorithm.Lz4;

    [Fact(DisplayName = "Lz4Compressor Instance singleton should not be null and have Lz4 algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        Lz4Compressor.Instance.ShouldNotBeNull();
        Lz4Compressor.Instance.Algorithm.ShouldBe(KyrolusCompressionAlgorithm.Lz4);
    }

    [Fact(DisplayName = "Decompress truncated LZ4 payload less than 4 bytes should throw InvalidOperationException")]
    public void Decompress_TruncatedPayloadLessThan4Bytes_ShouldThrowInvalidOperationException()
    {
        var corrupted = new byte[] { 1, 2 };

        var ex = Should.Throw<InvalidOperationException>(() =>
            Lz4Compressor.Instance.Decompress(corrupted));

        ex.Message.ShouldContain("header missing");
    }

    [Fact(DisplayName = "Decompress negative length header should throw InvalidOperationException")]
    public void Decompress_NegativeLengthHeader_ShouldThrowInvalidOperationException()
    {
        // 4 bytes indicating negative length -1 (0xFF, 0xFF, 0xFF, 0xFF)
        var corrupted = new byte[] { 255, 255, 255, 255, 1, 2, 3 };

        var ex = Should.Throw<InvalidOperationException>(() =>
            Lz4Compressor.Instance.Decompress(corrupted));

        ex.Message.ShouldContain("negative length");
    }

    [Fact(DisplayName = "Decompress corrupted LZ4 block should throw InvalidOperationException")]
    public void Decompress_CorruptedLz4Block_ShouldThrowInvalidOperationException()
    {
        // 4 bytes indicating original length of 500 bytes, followed by garbage bytes that fail decompression
        var corrupted = new byte[] { 244, 1, 0, 0, 255, 255, 255, 255 };

        var ex = Should.Throw<InvalidOperationException>(() =>
            Lz4Compressor.Instance.Decompress(corrupted));

        ex.Message.ShouldContain("failed");
    }

    [Theory(DisplayName = "Lz4Compressor with different compression levels should compress and decompress correctly")]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    [InlineData(CompressionLevel.NoCompression)]
    [InlineData((CompressionLevel)99)]
    public async Task Lz4_DifferentCompressionLevels_ShouldRoundtrip(CompressionLevel level)
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using var src = new MemoryStream(data);
        using var dst = new MemoryStream();

        await Lz4Compressor.Instance.CompressAsync(src, dst, level);
        dst.Position = 0;

        using var decomp = new MemoryStream();
        await Lz4Compressor.Instance.DecompressAsync(dst, decomp);

        decomp.ToArray().ShouldBe(data);
    }
}
