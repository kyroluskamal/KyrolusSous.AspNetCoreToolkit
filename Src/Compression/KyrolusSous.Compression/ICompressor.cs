using System.IO.Compression;

namespace KyrolusSous.Compression;

/// <summary>
/// Defines standard compression operations for byte arrays, streams, and strings.
/// </summary>
public interface ICompressor
{
    /// <summary>
    /// Gets the compression algorithm implemented by this compressor.
    /// </summary>
    CompressionAlgorithm Algorithm { get; }

    /// <summary>
    /// Compresses a raw byte array.
    /// </summary>
    /// <param name="data">The raw bytes to compress.</param>
    /// <param name="level">The compression level.</param>
    /// <returns>The compressed bytes.</returns>
    byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Fastest);

    /// <summary>
    /// Decompresses a compressed byte array.
    /// </summary>
    /// <param name="compressedData">The compressed bytes to restore.</param>
    /// <returns>The uncompressed raw bytes.</returns>
    byte[] Decompress(byte[] compressedData);

    /// <summary>
    /// Compresses a source stream into a destination stream asynchronously.
    /// </summary>
    /// <param name="source">The input stream containing raw data.</param>
    /// <param name="destination">The output stream to write compressed data to.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CompressAsync(
        Stream source,
        Stream destination,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decompresses a source stream into a destination stream asynchronously.
    /// </summary>
    /// <param name="source">The input stream containing compressed data.</param>
    /// <param name="destination">The output stream to write decompressed data to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compresses a UTF-8 string into a Base64-encoded string.
    /// </summary>
    /// <param name="text">The raw text to compress.</param>
    /// <param name="level">The compression level.</param>
    /// <returns>A Base64 string containing the compressed bytes.</returns>
    string CompressString(string text, CompressionLevel level = CompressionLevel.Fastest);

    /// <summary>
    /// Decompresses a Base64-encoded compressed string back to UTF-8 text.
    /// </summary>
    /// <param name="compressedBase64">The Base64 string containing compressed bytes.</param>
    /// <returns>The restored UTF-8 text.</returns>
    string DecompressString(string compressedBase64);
}
