using System.IO.Compression;
using KyrolusSous.Compression;

namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Payload transformer utilizing Google's Brotli compression for maximum compression ratio.
/// </summary>
public sealed class KyrolusBrotliCachePayloadTransformer(
    int minSizeBytes = 1024,
    CompressionLevel level = CompressionLevel.Fastest) : IKyrolusCachePayloadTransformer
{
    private const byte RawFlag = 0;
    private const byte CompressedFlag = 1;
    private static readonly byte[] Header = [(byte)'K', (byte)'Y', (byte)'C', (byte)'B'];

    private readonly int minSizeBytes = Math.Max(0, minSizeBytes);
    private readonly CompressionLevel level = level;

    public byte[] Transform(byte[] payload)
    {
        if (payload.Length < minSizeBytes)
        {
            return BuildRawPayload(payload);
        }

        var compressed = BrotliCompressor.Instance.Compress(payload, level);
        var result = new byte[Header.Length + 1 + compressed.Length];
        Buffer.BlockCopy(Header, 0, result, 0, Header.Length);
        result[Header.Length] = CompressedFlag;
        Buffer.BlockCopy(compressed, 0, result, Header.Length + 1, compressed.Length);
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

        if (flag != CompressedFlag)
        {
            return payload;
        }

        var compressed = SlicePayload(payload, Header.Length + 1);
        return BrotliCompressor.Instance.Decompress(compressed);
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
