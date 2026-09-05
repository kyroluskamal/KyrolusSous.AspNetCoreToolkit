namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class GzipCompressorTests : CompressorTestBase
{
    protected override IKyrolusCompressor Compressor => GzipCompressor.Instance;
    protected override KyrolusCompressionAlgorithm ExpectedAlgorithm => KyrolusCompressionAlgorithm.Gzip;

    [Fact(DisplayName = "GzipCompressor Instance singleton should not be null and have Gzip algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        GzipCompressor.Instance.ShouldNotBeNull();
        GzipCompressor.Instance.Algorithm.ShouldBe(KyrolusCompressionAlgorithm.Gzip);
    }
}
