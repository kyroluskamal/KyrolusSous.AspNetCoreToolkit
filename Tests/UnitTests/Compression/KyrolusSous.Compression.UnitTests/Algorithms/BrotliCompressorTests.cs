namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class BrotliCompressorTests : CompressorTestBase
{
    protected override IKyrolusCompressor Compressor => BrotliCompressor.Instance;
    protected override KyrolusCompressionAlgorithm ExpectedAlgorithm => KyrolusCompressionAlgorithm.Brotli;

    [Fact(DisplayName = "BrotliCompressor Instance singleton should not be null and have Brotli algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        BrotliCompressor.Instance.ShouldNotBeNull();
        BrotliCompressor.Instance.Algorithm.ShouldBe(KyrolusCompressionAlgorithm.Brotli);
    }
}
