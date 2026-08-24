using System.IO.Compression;
using KyrolusSous.Compression;

namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Generalized payload transformer supporting any <see cref="ICompressor"/> (Brotli, Zstd, LZ4, Snappy, Gzip, Deflate).
/// </summary>
public sealed class KyrolusCompressionCachePayloadTransformer : IKyrolusCachePayloadTransformer
{
    private const byte RawFlag = 0;
    private const byte CompressedFlag = 1;
    private static readonly byte[] Header = [(byte)'K', (byte)'Y', (byte)'C', (byte)'X'];

    private readonly ICompressor compressor;
    private readonly ICompressionProvider? provider;
    private readonly int minSizeBytes;
    private readonly CompressionLevel level;

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

    public byte[] Transform(byte[] payload)
    {
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

    public byte[] Restore(byte[] payload)
    {
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
