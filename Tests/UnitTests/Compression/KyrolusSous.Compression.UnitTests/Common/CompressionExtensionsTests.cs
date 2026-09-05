namespace KyrolusSous.Compression.UnitTests.Common;

public class CompressionExtensionsTests
{
    public CompressionExtensionsTests()
    {
        // Ensure singleton provider has Brotli and Gzip registered for extension tests
        KyrolusCompressionProvider.Instance.Register(BrotliCompressor.Instance);
        KyrolusCompressionProvider.Instance.Register(GzipCompressor.Instance);
    }

    [Fact(DisplayName = "ByteArray extensions Compress and Decompress should roundtrip payload and reduce size on text")]
    public void ByteArray_Extensions_ShouldCompressAndDecompress()
    {
        var text = string.Concat(Enumerable.Repeat("Kyrolus JSON Payload with repetitive text fields: {\"id\":10,\"status\":\"active\"}; ", 50));
        var original = Encoding.UTF8.GetBytes(text);

        var compressed = original.Compress(KyrolusCompressionAlgorithm.Brotli);
        compressed.ShouldNotBeEmpty();

        // Explicitly assert that compressed byte size is less than 30% of original
        compressed.Length.ShouldBeLessThan(original.Length / 3);

        var decompressed = compressed.Decompress(KyrolusCompressionAlgorithm.Brotli);
        decompressed.ShouldBe(original);
    }

    [Fact(DisplayName = "ReadOnlySpan extensions Compress and Decompress should roundtrip payload")]
    public void ReadOnlySpan_Extensions_ShouldCompressAndDecompress()
    {
        var original = Encoding.UTF8.GetBytes("Testing ReadOnlySpan compression extension methods.");
        ReadOnlySpan<byte> span = original;

        var compressed = span.Compress(KyrolusCompressionAlgorithm.Gzip);
        compressed.ShouldNotBeEmpty();

        ReadOnlySpan<byte> compressedSpan = compressed;
        var decompressed = compressedSpan.Decompress(KyrolusCompressionAlgorithm.Gzip);
        decompressed.ShouldBe(original);
    }

    [Fact(DisplayName = "String extensions CompressString and DecompressString should roundtrip Base64 and reduce text size")]
    public void String_Extensions_ShouldCompressAndDecompressBase64()
    {
        var rawPattern = "Testing Base64 string compression extensions with Unicode: أهلاً وسهلاً بكم 🚀. ";
        var originalText = string.Concat(Enumerable.Repeat(rawPattern, 100));

        var base64Compressed = originalText.CompressString(KyrolusCompressionAlgorithm.Brotli);
        base64Compressed.ShouldNotBeNullOrWhiteSpace();

        // Convert base64 back to compressed bytes to verify physical size shrunk significantly
        var compressedRawBytes = Convert.FromBase64String(base64Compressed);
        compressedRawBytes.Length.ShouldBeLessThan(Encoding.UTF8.GetByteCount(originalText) / 4);

        var decompressedText = base64Compressed.DecompressString(KyrolusCompressionAlgorithm.Brotli);
        decompressedText.ShouldBe(originalText);
    }

    [Fact(DisplayName = "String extensions with null input should throw ArgumentNullException")]
    public void String_Extensions_Null_ShouldThrowArgumentNullException()
    {
        string nullString = null!;

        Should.Throw<ArgumentNullException>(() => nullString.CompressString());
        Should.Throw<ArgumentNullException>(() => nullString.DecompressString());
    }

    [Fact(DisplayName = "Stream extensions CompressToStreamAsync and DecompressToStreamAsync should roundtrip payload and reduce stream size")]
    public async Task Stream_Extensions_ShouldCompressAndDecompress()
    {
        var jsonText = string.Concat(Enumerable.Repeat("{\"event\": \"UserLoggedIn\", \"service\": \"AuthService\", \"timestamp\": 1700000000}, ", 100));
        var original = Encoding.UTF8.GetBytes(jsonText);
        using var source = new MemoryStream(original);
        using var destination = new MemoryStream();

        await source.CompressToStreamAsync(destination, KyrolusCompressionAlgorithm.Brotli);

        // Verify compressed stream position/size is significantly smaller
        destination.Length.ShouldBeLessThan(original.Length / 3);

        destination.Position = 0;
        using var decompressed = new MemoryStream();
        await destination.DecompressToStreamAsync(decompressed, KyrolusCompressionAlgorithm.Brotli);

        decompressed.ToArray().ShouldBe(original);
    }
}
