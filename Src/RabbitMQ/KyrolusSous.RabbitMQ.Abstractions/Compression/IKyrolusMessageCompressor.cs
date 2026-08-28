namespace KyrolusSous.RabbitMQ.Abstractions.Compression;

/// <summary>
/// Abstraction for compressing and decompressing message payloads over RabbitMQ.
/// </summary>
public interface IKyrolusMessageCompressor
{
    /// <summary>
    /// Content encoding name (e.g., "gzip", "br", "zstd").
    /// </summary>
    string EncodingName { get; }

    byte[] Compress(byte[] rawBytes);
    byte[] Decompress(byte[] compressedBytes);
}
