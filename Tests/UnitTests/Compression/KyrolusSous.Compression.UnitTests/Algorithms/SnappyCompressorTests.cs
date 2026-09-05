namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class SnappyCompressorTests : CompressorTestBase
{
    protected override IKyrolusCompressor Compressor => SnappyCompressor.Instance;
    protected override KyrolusCompressionAlgorithm ExpectedAlgorithm => KyrolusCompressionAlgorithm.Snappy;

    [Fact(DisplayName = "SnappyCompressor Instance singleton should not be null and have Snappy algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        SnappyCompressor.Instance.ShouldNotBeNull();
        SnappyCompressor.Instance.Algorithm.ShouldBe(KyrolusCompressionAlgorithm.Snappy);
    }
}
