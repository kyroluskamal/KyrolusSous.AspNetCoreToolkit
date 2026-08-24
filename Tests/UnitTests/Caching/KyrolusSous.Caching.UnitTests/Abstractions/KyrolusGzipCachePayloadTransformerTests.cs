namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusGzipCachePayloadTransformerTests
{
    [Fact(DisplayName = "KyrolusGzipCachePayloadTransformer: Small payload below threshold should remain raw")]
    public void SmallPayload_BelowThreshold_StoredRaw()
    {
        var transformer = new KyrolusGzipCachePayloadTransformer(minSizeBytes: 1024);
        var smallPayload = Encoding.UTF8.GetBytes("Small raw text for Gzip < 1024 bytes");

        var transformed = transformer.Transform(smallPayload);
        transformed.Length.ShouldBe(4 + 1 + smallPayload.Length);

        var restored = transformer.Restore(transformed);
        restored.ShouldBe(smallPayload);
    }

    [Fact(DisplayName = "KyrolusGzipCachePayloadTransformer: Large payload above threshold MUST compress and physically reduce byte size")]
    public void LargePayload_AboveThreshold_CompressesAndReducesSize()
    {
        var transformer = new KyrolusGzipCachePayloadTransformer(minSizeBytes: 500);

        var sb = new StringBuilder();
        for (var i = 0; i < 200; i++)
        {
            sb.Append($"Row {i}: This is structured repetitive database record payload text for testing compression. ");
        }
        var originalBytes = Encoding.UTF8.GetBytes(sb.ToString());
        originalBytes.Length.ShouldBeGreaterThan(5000);

        var transformed = transformer.Transform(originalBytes);

        // Physical size reduction assertion
        transformed.Length.ShouldBeLessThan(originalBytes.Length);
        var ratio = (double)transformed.Length / originalBytes.Length;
        ratio.ShouldBeLessThan(0.40); // Gzip should compress by > 60%

        var restoredBytes = transformer.Restore(transformed);
        restoredBytes.ShouldBe(originalBytes);
    }

    [Fact(DisplayName = "KyrolusGzipCachePayloadTransformer: Null payload should throw ArgumentNullException")]
    public void NullPayload_ThrowsArgumentNullException()
    {
        var transformer = new KyrolusGzipCachePayloadTransformer();
        Should.Throw<ArgumentNullException>(() => transformer.Transform(null!));
        Should.Throw<ArgumentNullException>(() => transformer.Restore(null!));
    }

    [Fact(DisplayName = "KyrolusGzipCachePayloadTransformer: Unknown flag byte should return payload as-is")]
    public void UnknownFlag_ReturnsAsIs()
    {
        var transformer = new KyrolusGzipCachePayloadTransformer();
        var unknownFlagPayload = new byte[] { (byte)'K', (byte)'Y', (byte)'C', (byte)'0', 99, 1, 2, 3 };

        var restored = transformer.Restore(unknownFlagPayload);
        restored.ShouldBe(unknownFlagPayload);
    }

    [Fact(DisplayName = "KyrolusGzipCachePayloadTransformer: Payload without KYC0 header should return as-is")]
    public void Payload_WithoutHeader_ReturnsAsIs()
    {
        var transformer = new KyrolusGzipCachePayloadTransformer();
        var legacy = Encoding.UTF8.GetBytes("Legacy data");
        transformer.Restore(legacy).ShouldBe(legacy);
    }
}
