namespace KyrolusSous.Compression.UnitTests.Algorithms;

public class BrotliCompressorTests : CompressorTestBase
{
    protected override ICompressor Compressor => BrotliCompressor.Instance;
    protected override CompressionAlgorithm ExpectedAlgorithm => CompressionAlgorithm.Brotli;

    [Fact(DisplayName = "BrotliCompressor Instance singleton should not be null and have Brotli algorithm")]
    public void Instance_ShouldNotBeNull()
    {
        BrotliCompressor.Instance.ShouldNotBeNull();
        BrotliCompressor.Instance.Algorithm.ShouldBe(CompressionAlgorithm.Brotli);
    }
}
