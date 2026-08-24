namespace KyrolusSous.Compression.UnitTests.Common;

public abstract class CompressorTestBase
{
    protected abstract ICompressor Compressor { get; }
    protected abstract CompressionAlgorithm ExpectedAlgorithm { get; }

    [Fact(DisplayName = "Algorithm should match the expected compression algorithm")]
    public void Algorithm_ShouldMatchExpectedAlgorithm()
    {
        Compressor.Algorithm.ShouldBe(ExpectedAlgorithm);
    }

    [Fact(DisplayName = "Compress and Decompress with empty byte array should return empty array")]
    public void Compress_And_Decompress_EmptyArray_ShouldReturnEmptyArray()
    {
        var compressed = Compressor.Compress(ReadOnlySpan<byte>.Empty);
        compressed.ShouldBeEmpty();

        var decompressed = Compressor.Decompress(ReadOnlySpan<byte>.Empty);
        decompressed.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Compress and Decompress with small payload should roundtrip successfully")]
    public void Compress_And_Decompress_SmallPayload_ShouldRoundtripSuccessfully()
    {
        var original = Encoding.UTF8.GetBytes("Hello, Kyrolus Compression World! Testing small payload.");

        var compressed = Compressor.Compress(original);
        compressed.ShouldNotBeEmpty();

        var decompressed = Compressor.Decompress(compressed);
        decompressed.ShouldBe(original);
    }

    [Fact(DisplayName = "Compress and Decompress with large repetitive JSON data should achieve significant compression")]
    public void Compress_And_Decompress_LargeRepetitivePayload_ShouldCompressSignificantly()
    {
        // 100 KB of repetitive JSON data
        var jsonPattern = "{\"id\": 12345, \"name\": \"Kyrolus Sous\", \"status\": \"Active\", \"role\": \"Architect\"},\n";
        var repeatedText = string.Concat(Enumerable.Repeat(jsonPattern, 1200));
        var original = Encoding.UTF8.GetBytes(repeatedText);

        var compressed = Compressor.Compress(original);

        // Compressed payload must be significantly smaller than original uncompressed text
        compressed.Length.ShouldBeLessThan(original.Length / 2);

        var decompressed = Compressor.Decompress(compressed);
        decompressed.ShouldBe(original);
    }

    [Fact(DisplayName = "Compress with realistic HTML and JSON text document should reduce size by at least 60 percent")]
    public void Compress_RealisticTextDocument_ShouldReduceSizeSignificantly()
    {
        var htmlContent = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <title>Kyrolus Enterprise Toolkit - High Performance Architecture</title>
                <style>
                    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 20px; }
                    .card { border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin-bottom: 12px; background: #ffffff; }
                    .header { font-size: 24px; font-weight: bold; color: #1a202c; }
                    .badge { display: inline-block; padding: 4px 8px; border-radius: 4px; background: #edf2f7; font-size: 12px; }
                </style>
            </head>
            <body>
                <div class="header">System Performance Dashboard</div>
                <div class="card">
                    <h2>Modular Architecture Overview</h2>
                    <p>KyrolusSous.AspNetCoreToolkit is engineered with extreme performance, zero allocations, and modular scalability.</p>
                </div>
            </body>
            </html>
            """;

        var largeDocument = string.Concat(Enumerable.Repeat(htmlContent, 100));
        var originalBytes = Encoding.UTF8.GetBytes(largeDocument);

        var compressedBytes = Compressor.Compress(originalBytes);

        // Verify the size actually shrunk drastically (at least 60% reduction, meaning compressed is < 40% of original)
        compressedBytes.Length.ShouldBeLessThan((int)(originalBytes.Length * 0.40));

        // Verify exact lossless restoration
        var restoredBytes = Compressor.Decompress(compressedBytes);
        restoredBytes.ShouldBe(originalBytes);
    }

    [Fact(DisplayName = "Compress and Decompress with binary pseudo-random payload should roundtrip successfully")]
    public void Compress_And_Decompress_BinaryPayload_ShouldRoundtripSuccessfully()
    {
        var random = new Random(42);
        var original = new byte[8192];
        random.NextBytes(original);

        var compressed = Compressor.Compress(original);
        compressed.ShouldNotBeEmpty();

        var decompressed = Compressor.Decompress(compressed);
        decompressed.ShouldBe(original);
    }

    [Theory(DisplayName = "CompressAsync and DecompressAsync with Streams across different levels should roundtrip")]
    [InlineData(CompressionLevel.Fastest)]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    [InlineData(CompressionLevel.NoCompression)]
    public async Task CompressAsync_And_DecompressAsync_Stream_ShouldRoundtripSuccessfully(CompressionLevel level)
    {
        var originalText = "Streaming compression test with Kyrolus toolkit! " + new string('X', 5000);
        var originalBytes = Encoding.UTF8.GetBytes(originalText);

        using var sourceStream = new MemoryStream(originalBytes);
        using var compressedStream = new MemoryStream();

        await Compressor.CompressAsync(sourceStream, compressedStream, level);
        compressedStream.Position = 0;

        using var decompressedStream = new MemoryStream();
        await Compressor.DecompressAsync(compressedStream, decompressedStream);

        decompressedStream.ToArray().ShouldBe(originalBytes);
    }

    [Fact(DisplayName = "CreateCompressionStream and CreateDecompressionStream should wrap streams correctly")]
    public void CreateCompressionStream_And_CreateDecompressionStream_ShouldWorkCorrectly()
    {
        var original = Encoding.UTF8.GetBytes("Testing direct stream wrapping via CreateCompressionStream.");

        using var compressedMemory = new MemoryStream();
        using (var compressionStream = Compressor.CreateCompressionStream(compressedMemory, CompressionLevel.Fastest, leaveOpen: true))
        {
            compressionStream.Write(original, 0, original.Length);
            compressionStream.Flush();
        }

        compressedMemory.Position = 0;

        using var decompressedMemory = new MemoryStream();
        using (var decompressionStream = Compressor.CreateDecompressionStream(compressedMemory, leaveOpen: true))
        {
            decompressionStream.CopyTo(decompressedMemory);
        }

        decompressedMemory.ToArray().ShouldBe(original);
    }

    [Fact(DisplayName = "CompressAsync and DecompressAsync with null stream parameters should throw ArgumentNullException")]
    public async Task CompressAsync_NullStreams_ShouldThrowArgumentNullException()
    {
        using var validStream = new MemoryStream();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await Compressor.CompressAsync(null!, validStream));

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await Compressor.CompressAsync(validStream, null!));

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await Compressor.DecompressAsync(null!, validStream));

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await Compressor.DecompressAsync(validStream, null!));

        Should.Throw<ArgumentNullException>(() =>
            Compressor.CreateCompressionStream(null!));

        Should.Throw<ArgumentNullException>(() =>
            Compressor.CreateDecompressionStream(null!));
    }
}
