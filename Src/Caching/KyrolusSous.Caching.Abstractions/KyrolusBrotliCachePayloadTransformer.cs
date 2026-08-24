namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// A high-performance payload transformer utilizing Google's Brotli compression algorithm for maximum compression ratio.
/// Built exclusively on native .NET BCL <see cref="BrotliStream"/> with zero external dependencies.
/// </summary>
/// <remarks>
/// <para>
/// <b>Payload Framing Format:</b>
/// <c>[4-byte Magic Header 'KYCB'][1-byte Flag (0=Raw, 1=Compressed)][Payload Body]</c>
/// </para>
/// <para>
/// <b>Threshold Optimization:</b>
/// If the serialized payload size is smaller than <paramref name="minSizeBytes"/>, compression is skipped 
/// to avoid wasting CPU cycles and CPU overhead on tiny objects. The payload is stored with the <c>RawFlag</c> (0).
/// </para>
/// </remarks>
/// <param name="minSizeBytes">Minimum payload byte length required to trigger compression. Defaults to 1024 bytes (1 KB).</param>
/// <param name="level">The compression quality/speed tradeoff level. Defaults to <see cref="CompressionLevel.Fastest"/>.</param>
public sealed class KyrolusBrotliCachePayloadTransformer(
    int minSizeBytes = 1024,
    CompressionLevel level = CompressionLevel.Fastest) : IKyrolusCachePayloadTransformer
{
    private const byte RawFlag = 0;
    private const byte CompressedFlag = 1;
    private static readonly byte[] Header = [(byte)'K', (byte)'Y', (byte)'C', (byte)'B'];

    private readonly int minSizeBytes = Math.Max(0, minSizeBytes);
    private readonly CompressionLevel level = level;

    /// <summary>
    /// Compresses the serialized byte payload using Brotli if size meets or exceeds <c>minSizeBytes</c>.
    /// </summary>
    /// <param name="payload">The original serialized byte array.</param>
    /// <returns>A framed byte array containing the 'KYCB' header, compression flag, and the data.</returns>
    public byte[] Transform(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Length < minSizeBytes)
        {
            return BuildRawPayload(payload);
        }

        using var memoryStream = new MemoryStream();
        using (var brotliStream = new BrotliStream(memoryStream, level, leaveOpen: true))
        {
            brotliStream.Write(payload, 0, payload.Length);
        }

        var compressed = memoryStream.ToArray();
        var result = new byte[Header.Length + 1 + compressed.Length];
        Buffer.BlockCopy(Header, 0, result, 0, Header.Length);
        result[Header.Length] = CompressedFlag;
        Buffer.BlockCopy(compressed, 0, result, Header.Length + 1, compressed.Length);
        return result;
    }

    /// <summary>
    /// Decompresses the Brotli-compressed byte array back to the original serialized payload.
    /// </summary>
    /// <param name="payload">The framed byte array read from cache.</param>
    /// <returns>The uncompressed original serialized byte array.</returns>
    public byte[] Restore(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!HasHeader(payload))
        {
            return payload;
        }

        var flag = payload[Header.Length];
        if (flag == RawFlag)
        {
            return SlicePayload(payload, Header.Length + 1);
        }

        if (flag != CompressedFlag)
        {
            return payload;
        }

        var compressed = SlicePayload(payload, Header.Length + 1);
        using var memoryStream = new MemoryStream(compressed);
        using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        brotliStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    private static bool HasHeader(byte[] payload)
    {
        if (payload.Length <= Header.Length)
        {
            return false;
        }

        for (var i = 0; i < Header.Length; i++)
        {
            if (payload[i] != Header[i])
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] BuildRawPayload(byte[] payload)
    {
        var result = new byte[Header.Length + 1 + payload.Length];
        Buffer.BlockCopy(Header, 0, result, 0, Header.Length);
        result[Header.Length] = RawFlag;
        Buffer.BlockCopy(payload, 0, result, Header.Length + 1, payload.Length);
        return result;
    }

    private static byte[] SlicePayload(byte[] payload, int offset)
    {
        if (offset >= payload.Length)
        {
            return [];
        }

        var result = new byte[payload.Length - offset];
        Buffer.BlockCopy(payload, offset, result, 0, result.Length);
        return result;
    }
}
