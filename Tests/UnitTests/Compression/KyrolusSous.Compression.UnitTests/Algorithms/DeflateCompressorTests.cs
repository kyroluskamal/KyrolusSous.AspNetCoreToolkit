namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class DeflateCompressorTests : CompressorTestBase
{
    protected override ICompressor Compressor => DeflateCompressor.Instance;
    protected override CompressionAlgorithm ExpectedAlgorithm => CompressionAlgorithm.Deflate;

    [Fact(DisplayName = "DeflateCompressor Instance singleton should not be null and have Deflate algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        DeflateCompressor.Instance.ShouldNotBeNull();
        DeflateCompressor.Instance.Algorithm.ShouldBe(CompressionAlgorithm.Deflate);
    }
}
