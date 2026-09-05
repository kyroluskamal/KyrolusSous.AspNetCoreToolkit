using System.IO.Compression;
using KyrolusSous.Compression;

namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// A payload transformer utilizing any registered <see cref="IKyrolusCompressor"/> or <see cref="IKyrolusCompressionProvider"/>
/// (e.g. GZip, Brotli, Snappy, Zstd, LZ4, Deflate).
/// </summary>
public class KyrolusCompressionCachePayloadTransformer : IKyrolusCachePayloadTransformer
{
    private const byte RawFlag = 0;
    private const byte CompressedFlag = 1;
    private static readonly byte[] Header = [(byte)'K', (byte)'Y', (byte)'C', (byte)'X'];

    private readonly IKyrolusCompressor _compressor;
    private readonly IKyrolusCompressionProvider? _provider;
    private readonly int _minSizeBytes;
    private readonly CompressionLevel _level;

    public KyrolusCompressionCachePayloadTransformer(
        IKyrolusCompressor compressor,
        IKyrolusCompressionProvider? provider = null,
        int minSizeBytes = 1024,
        CompressionLevel level = CompressionLevel.Fastest)
    {
        _compressor = compressor ?? throw new ArgumentNullException(nameof(compressor));
        _provider = provider;
        _minSizeBytes = Math.Max(0, minSizeBytes);
        _level = level;
    }

    public byte[] Transform(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Length < _minSizeBytes)
        {
            return BuildRawPayload(payload);
        }

        var compressed = _compressor.Compress(payload);
        var result = new byte[Header.Length + 2 + compressed.Length];
        Buffer.BlockCopy(Header, 0, result, 0, Header.Length);
        result[Header.Length] = CompressedFlag;
        result[Header.Length + 1] = (byte)_compressor.Algorithm;
        Buffer.BlockCopy(compressed, 0, result, Header.Length + 2, compressed.Length);
        return result;
    }

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

        if (payload.Length < Header.Length + 2)
        {
            return payload;
        }

        var algorithmByte = payload[Header.Length + 1];
        var compressedSpan = payload.AsSpan(Header.Length + 2);

        if (_provider is not null && _provider.TryGetCompressor((KyrolusCompressionAlgorithm)algorithmByte, out var matchingCompressor) && matchingCompressor is not null)
        {
            return matchingCompressor.Decompress(compressedSpan);
        }

        return _compressor.Decompress(compressedSpan);
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
