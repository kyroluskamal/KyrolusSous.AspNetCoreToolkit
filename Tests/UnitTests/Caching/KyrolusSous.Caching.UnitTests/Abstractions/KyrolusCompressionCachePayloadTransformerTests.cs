namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusCompressionCachePayloadTransformerTests
{
    private static byte[] GenerateLargePayload()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 200; i++)
        {
            sb.Append($"{{\"id\":{i},\"title\":\"Article Number {i}\",\"content\":\"Long text content for compression testing.\"}},");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    [Theory(DisplayName = "KyrolusCompressionCachePayloadTransformer: Supported compressors MUST compress and physically reduce byte size")]
    [InlineData(CompressionAlgorithm.Brotli)]
    [InlineData(CompressionAlgorithm.Gzip)]
    [InlineData(CompressionAlgorithm.Zstd)]
    [InlineData(CompressionAlgorithm.Lz4)]
    [InlineData(CompressionAlgorithm.Snappy)]
    public void Compressors_Compress_And_PhysicallyReduceSize(CompressionAlgorithm algorithm)
    {
        ICompressor compressor = algorithm switch
        {
            CompressionAlgorithm.Brotli => new BrotliCompressor(),
            CompressionAlgorithm.Gzip => new GzipCompressor(),
            CompressionAlgorithm.Zstd => new ZstdCompressor(),
            CompressionAlgorithm.Lz4 => new Lz4Compressor(),
            CompressionAlgorithm.Snappy => new SnappyCompressor(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };

        var transformer = new KyrolusCompressionCachePayloadTransformer(compressor, minSizeBytes: 500);
        var originalBytes = GenerateLargePayload();
        originalBytes.Length.ShouldBeGreaterThan(5000);

        var transformed = transformer.Transform(originalBytes);

        // Physical size reduction assertion
        transformed.Length.ShouldBeLessThan(originalBytes.Length);
        var ratio = (double)transformed.Length / originalBytes.Length;
        ratio.ShouldBeLessThan(0.50); // Must be at least 50% smaller

        // Header check ('KYCX' + CompressedFlag + Algorithm Byte)
        transformed[0].ShouldBe((byte)'K');
        transformed[1].ShouldBe((byte)'Y');
        transformed[2].ShouldBe((byte)'C');
        transformed[3].ShouldBe((byte)'X');
        transformed[4].ShouldBe((byte)1); // CompressedFlag
        transformed[5].ShouldBe((byte)algorithm);

        // Decompress
        var restored = transformer.Restore(transformed);
        restored.ShouldBe(originalBytes);
    }

    [Fact(DisplayName = "KyrolusCompressionCachePayloadTransformer: Null compressor should throw ArgumentNullException")]
    public void NullCompressor_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new KyrolusCompressionCachePayloadTransformer(null!));
    }

    [Fact(DisplayName = "KyrolusCompressionCachePayloadTransformer: Small payload below threshold should remain raw")]
    public void SmallPayload_BelowThreshold_StoredRaw()
    {
        var compressor = new BrotliCompressor();
        var transformer = new KyrolusCompressionCachePayloadTransformer(compressor, minSizeBytes: 1024);
        var smallPayload = Encoding.UTF8.GetBytes("Small payload");

        var transformed = transformer.Transform(smallPayload);
        transformed.Length.ShouldBe(4 + 1 + smallPayload.Length);

        var restored = transformer.Restore(transformed);
        restored.ShouldBe(smallPayload);
    }

    [Fact(DisplayName = "KyrolusCompressionCachePayloadTransformer: Legacy data without KYCX header should return as-is")]
    public void LegacyData_ReturnsAsIs()
    {
        var compressor = new BrotliCompressor();
        var transformer = new KyrolusCompressionCachePayloadTransformer(compressor);
        var legacy = Encoding.UTF8.GetBytes("Legacy data");

        transformer.Restore(legacy).ShouldBe(legacy);
    }

    [Fact(DisplayName = "KyrolusCompressionCachePayloadTransformer: Unknown flag byte should return payload as-is")]
    public void UnknownFlag_ReturnsAsIs()
    {
        var compressor = new BrotliCompressor();
        var transformer = new KyrolusCompressionCachePayloadTransformer(compressor);
        var corrupted = new byte[] { (byte)'K', (byte)'Y', (byte)'C', (byte)'X', 99, (byte)CompressionAlgorithm.Brotli, 1, 2, 3 };

        var restored = transformer.Restore(corrupted);
        restored.ShouldBe(corrupted);
    }

    [Fact(DisplayName = "KyrolusCompressionCachePayloadTransformer: Dynamic decompression via ICompressionProvider")]
    public void DynamicDecompression_ViaProvider()
    {
        var provider = new KyrolusCompressionProvider();
        provider.Register(new BrotliCompressor());
        provider.Register(new GzipCompressor());

        var brotliTransformer = new KyrolusCompressionCachePayloadTransformer(new BrotliCompressor(), provider, minSizeBytes: 10);
        var gzipTransformer = new KyrolusCompressionCachePayloadTransformer(new GzipCompressor(), provider, minSizeBytes: 10);

        var original = GenerateLargePayload();

        // Compress with Gzip
        var gzipBytes = gzipTransformer.Transform(original);

        // Decompress with Brotli transformer that has provider access
        var restored = brotliTransformer.Restore(gzipBytes);
        restored.ShouldBe(original);
    }
}
