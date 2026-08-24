namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// A generalized, algorithm-agnostic payload transformer supporting any <see cref="ICompressor"/> 
/// (Brotli, Zstd, LZ4, Snappy, Gzip, Deflate) with dynamic algorithm tagging.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dynamic Framing Format:</b>
/// <c>[4-byte Magic Header 'KYCX'][1-byte Flag (0=Raw, 1=Compressed)][1-byte Algorithm ID][Payload Body]</c>
/// </para>
/// <para>
/// <b>Cross-Algorithm Interoperability:</b>
/// Because the algorithm ID is embedded directly in the header, if a cache cluster has mixed entries 
/// compressed with different algorithms (e.g. legacy Gzip entries and new Zstd/Brotli entries), 
/// the transformer dynamically resolves the appropriate decompressor via <see cref="ICompressionProvider"/>.
/// </para>
/// </remarks>
public sealed class KyrolusCompressionCachePayloadTransformer : IKyrolusCachePayloadTransformer
{
    private const byte RawFlag = 0;
    private const byte CompressedFlag = 1;
    private static readonly byte[] Header = [(byte)'K', (byte)'Y', (byte)'C', (byte)'X'];

    private readonly ICompressor compressor;
    private readonly ICompressionProvider? provider;
    private readonly int minSizeBytes;
    private readonly CompressionLevel level;

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusCompressionCachePayloadTransformer"/>.
    /// </summary>
    /// <param name="compressor">The primary compressor implementation to use for outgoing cache writes.</param>
    /// <param name="provider">Optional provider used to resolve compressors dynamically when decompressing different algorithm tags.</param>
    /// <param name="minSizeBytes">Minimum payload byte length before compression is triggered. Defaults to 1024 bytes (1 KB).</param>
    /// <param name="level">The compression quality/speed tradeoff level. Defaults to <see cref="CompressionLevel.Fastest"/>.</param>
    public KyrolusCompressionCachePayloadTransformer(
        ICompressor compressor,
        ICompressionProvider? provider = null,
        int minSizeBytes = 1024,
        CompressionLevel level = CompressionLevel.Fastest)
    {
        this.compressor = compressor ?? throw new ArgumentNullException(nameof(compressor));
        this.provider = provider;
        this.minSizeBytes = Math.Max(0, minSizeBytes);
        this.level = level;
    }

    /// <summary>
    /// Compresses the payload using the configured compressor and encodes the algorithm tag in the header.
    /// </summary>
    /// <param name="payload">The original serialized byte array.</param>
    /// <returns>A framed byte array containing the 'KYCX' header, flag, algorithm ID, and compressed bytes.</returns>
    public byte[] Transform(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Length < minSizeBytes)
        {
            return BuildRawPayload(payload);
        }

        var compressed = compressor.Compress(payload);
        var result = new byte[Header.Length + 2 + compressed.Length];
        Buffer.BlockCopy(Header, 0, result, 0, Header.Length);
        result[Header.Length] = CompressedFlag;
        result[Header.Length + 1] = (byte)compressor.Algorithm;
        Buffer.BlockCopy(compressed, 0, result, Header.Length + 2, compressed.Length);
        return result;
    }

    /// <summary>
    /// Decodes the header, identifies the compression algorithm used, and decompresses the payload.
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

        if (flag != CompressedFlag || payload.Length < Header.Length + 2)
        {
            return payload;
        }

        var algorithmByte = payload[Header.Length + 1];
        var algorithm = (CompressionAlgorithm)algorithmByte;

        var comp = (algorithm == compressor.Algorithm)
            ? compressor
            : (provider?.GetCompressor(algorithm) ?? compressor);

        var compressed = SlicePayload(payload, Header.Length + 2);
        return comp.Decompress(compressed);
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
