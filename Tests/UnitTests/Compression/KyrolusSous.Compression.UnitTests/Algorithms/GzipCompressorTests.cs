namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class GzipCompressorTests : CompressorTestBase
{
    protected override ICompressor Compressor => GzipCompressor.Instance;
    protected override CompressionAlgorithm ExpectedAlgorithm => CompressionAlgorithm.Gzip;

    [Fact(DisplayName = "GzipCompressor Instance singleton should not be null and have Gzip algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        GzipCompressor.Instance.ShouldNotBeNull();
        GzipCompressor.Instance.Algorithm.ShouldBe(CompressionAlgorithm.Gzip);
    }
}
