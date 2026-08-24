namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusBrotliCachePayloadTransformerTests
{
    [Fact(DisplayName = "KyrolusBrotliCachePayloadTransformer: Small payload below threshold should remain raw")]
    public void SmallPayload_BelowThreshold_StoredRaw()
    {
        var transformer = new KyrolusBrotliCachePayloadTransformer(minSizeBytes: 1024);
        var smallPayload = Encoding.UTF8.GetBytes("Small raw text < 1024 bytes");

        var transformed = transformer.Transform(smallPayload);
        transformed.Length.ShouldBe(4 + 1 + smallPayload.Length); // Header (4) + RawFlag (1) + Payload

        var restored = transformer.Restore(transformed);
        restored.ShouldBe(smallPayload);
    }

    [Fact(DisplayName = "KyrolusBrotliCachePayloadTransformer: Large payload above threshold MUST compress and physically reduce byte size")]
    public void LargePayload_AboveThreshold_CompressesAndReducesSize()
    {
        var transformer = new KyrolusBrotliCachePayloadTransformer(minSizeBytes: 500);

        // Generate large repetitive JSON text of 10,000 bytes
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < 200; i++)
        {
            sb.Append($"{{\"id\":{i},\"name\":\"Product Number {i}\",\"category\":\"Electronics & Computers\",\"inStock\":true}},");
        }
        sb.Append("{\"id\":999,\"name\":\"Final Product\",\"category\":\"Electronics\",\"inStock\":false}]");
        var originalText = sb.ToString();
        var originalBytes = Encoding.UTF8.GetBytes(originalText);

        originalBytes.Length.ShouldBeGreaterThan(5000);

        // Transform (Compress)
        var transformed = transformer.Transform(originalBytes);

        // Explicit physical size reduction verification
        transformed.Length.ShouldBeLessThan(originalBytes.Length);
        var compressionRatio = (double)transformed.Length / originalBytes.Length;
        compressionRatio.ShouldBeLessThan(0.35); // Brotli should compress JSON by > 65%

        // Restore (Decompress)
        var restoredBytes = transformer.Restore(transformed);
        restoredBytes.ShouldBe(originalBytes);
        Encoding.UTF8.GetString(restoredBytes).ShouldBe(originalText);
    }

    [Fact(DisplayName = "KyrolusBrotliCachePayloadTransformer: Payload without KYCB header should return as-is")]
    public void Payload_WithoutHeader_ReturnsAsIs()
    {
        var transformer = new KyrolusBrotliCachePayloadTransformer();
        var legacyPayload = Encoding.UTF8.GetBytes("Legacy non-framed data");

        var restored = transformer.Restore(legacyPayload);
        restored.ShouldBe(legacyPayload);
    }

    [Fact(DisplayName = "KyrolusBrotliCachePayloadTransformer: Null payload should throw ArgumentNullException")]
    public void NullPayload_ThrowsArgumentNullException()
    {
        var transformer = new KyrolusBrotliCachePayloadTransformer();
        Should.Throw<ArgumentNullException>(() => transformer.Transform(null!));
        Should.Throw<ArgumentNullException>(() => transformer.Restore(null!));
    }

    [Fact(DisplayName = "KyrolusBrotliCachePayloadTransformer: Unknown flag byte should return payload as-is")]
    public void UnknownFlag_ReturnsAsIs()
    {
        var transformer = new KyrolusBrotliCachePayloadTransformer();
        var unknownFlagPayload = new byte[] { (byte)'K', (byte)'Y', (byte)'C', (byte)'B', 99, 1, 2, 3 };

        var restored = transformer.Restore(unknownFlagPayload);
        restored.ShouldBe(unknownFlagPayload);
    }
}
