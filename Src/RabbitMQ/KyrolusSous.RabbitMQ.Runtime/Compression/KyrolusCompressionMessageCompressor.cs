using KyrolusSous.Compression;
using KyrolusSous.RabbitMQ.Abstractions.Compression;

namespace KyrolusSous.RabbitMQ.Runtime.Compression;

/// <summary>
/// Message compressor adapter that directly delegates to the toolkit's unified <see cref="ICompressor"/> implementations.
/// </summary>
public class KyrolusCompressionMessageCompressor : IKyrolusMessageCompressor
{
    private readonly ICompressor _compressor;

    public string EncodingName => _compressor.Algorithm.ToString().ToLowerInvariant();

    public KyrolusCompressionMessageCompressor(ICompressor compressor)
    {
        _compressor = compressor ?? throw new ArgumentNullException(nameof(compressor));
    }

    public byte[] Compress(byte[] rawBytes)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);
        return _compressor.Compress(rawBytes);
    }

    public byte[] Decompress(byte[] compressedBytes)
    {
        ArgumentNullException.ThrowIfNull(compressedBytes);
        return _compressor.Decompress(compressedBytes);
    }
}
