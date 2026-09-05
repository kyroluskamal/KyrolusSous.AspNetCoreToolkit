namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class DeflateCompressorTests : CompressorTestBase
{
    protected override IKyrolusCompressor Compressor => DeflateCompressor.Instance;
    protected override KyrolusCompressionAlgorithm ExpectedAlgorithm => KyrolusCompressionAlgorithm.Deflate;

    [Fact(DisplayName = "DeflateCompressor Instance singleton should not be null and have Deflate algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        DeflateCompressor.Instance.ShouldNotBeNull();
        DeflateCompressor.Instance.Algorithm.ShouldBe(KyrolusCompressionAlgorithm.Deflate);
    }
}
