namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class SnappyCompressorTests : CompressorTestBase
{
    protected override ICompressor Compressor => SnappyCompressor.Instance;
    protected override CompressionAlgorithm ExpectedAlgorithm => CompressionAlgorithm.Snappy;

    [Fact(DisplayName = "SnappyCompressor Instance singleton should not be null and have Snappy algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        SnappyCompressor.Instance.ShouldNotBeNull();
        SnappyCompressor.Instance.Algorithm.ShouldBe(CompressionAlgorithm.Snappy);
    }
}
