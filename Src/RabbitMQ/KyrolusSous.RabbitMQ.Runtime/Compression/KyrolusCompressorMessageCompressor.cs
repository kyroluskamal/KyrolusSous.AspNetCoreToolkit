using KyrolusSous.Compression;
using KyrolusSous.RabbitMQ.Abstractions.Compression;

namespace KyrolusSous.RabbitMQ.Runtime.Compression;

/// <summary>
/// Universal message compressor bridging any Kyrolus <see cref="IKyrolusCompressor"/> (GZip, Brotli, Snappy, Zstd, LZ4, Deflate)
/// to RabbitMQ messaging pipeline with decompression bomb protection.
/// </summary>
public class KyrolusCompressorMessageCompressor(
    IKyrolusCompressor compressor,
    long maxDecompressedBytes = 50 * 1024 * 1024) : IKyrolusMessageCompressor
{
    private readonly IKyrolusCompressor _compressor = compressor ?? throw new ArgumentNullException(nameof(compressor));
    private readonly long _maxDecompressedBytes = Math.Max(1024, maxDecompressedBytes);

    public string EncodingName => _compressor.Algorithm.ToString().ToLowerInvariant();

    public byte[] Compress(byte[] rawBytes)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);
        return _compressor.Compress(rawBytes);
    }

    public byte[] Decompress(byte[] compressedBytes)
    {
        ArgumentNullException.ThrowIfNull(compressedBytes);
        var decompressed = _compressor.Decompress(compressedBytes);
        if (decompressed.Length > _maxDecompressedBytes)
        {
            throw new InvalidOperationException($"Decompressed payload exceeds maximum allowable size of {_maxDecompressedBytes} bytes (Decompression bomb protection).");
        }
        return decompressed;
    }
}
