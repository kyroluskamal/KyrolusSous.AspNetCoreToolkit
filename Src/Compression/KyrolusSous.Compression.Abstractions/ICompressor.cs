namespace KyrolusSous.Compression;

/// <summary>
/// Defines standard compression operations for byte arrays, streams, and strings.
/// </summary>
public interface IKyrolusCompressor
{
    /// <summary>
    /// Gets the algorithm implemented by this compressor instance.
    /// </summary>
    KyrolusCompressionAlgorithm Algorithm { get; }

    /// <summary>
    /// Compresses uncompressed data into a compressed byte array.
    /// </summary>
    /// <param name="data">The raw bytes to compress.</param>
    /// <returns>The compressed byte array.</returns>
    byte[] Compress(ReadOnlySpan<byte> data);

    /// <summary>
    /// Decompresses compressed data back to original bytes.
    /// </summary>
    /// <param name="compressedData">The compressed bytes to decompress.</param>
    /// <returns>The decompressed byte array.</returns>
    byte[] Decompress(ReadOnlySpan<byte> compressedData);

    /// <summary>
    /// Compresses a stream asynchronously into an output stream.
    /// </summary>
    /// <param name="source">The uncompressed input stream.</param>
    /// <param name="destination">The destination stream to write compressed data to.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task CompressAsync(
        Stream source,
        Stream destination,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decompresses a stream asynchronously into an output stream.
    /// </summary>
    /// <param name="source">The compressed input stream.</param>
    /// <param name="destination">The destination stream to write decompressed data to.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a wrapping compression stream for writing compressed data to the underlying stream.
    /// </summary>
    /// <param name="outputStream">The target stream to write compressed data into.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="leaveOpen">Whether to leave the target stream open when disposing.</param>
    /// <returns>A writable compression stream.</returns>
    Stream CreateCompressionStream(
        Stream outputStream,
        CompressionLevel level = CompressionLevel.Fastest,
        bool leaveOpen = false);

    /// <summary>
    /// Creates a wrapping decompression stream for reading decompressed data from the underlying stream.
    /// </summary>
    /// <param name="inputStream">The source stream to read compressed data from.</param>
    /// <param name="leaveOpen">Whether to leave the source stream open when disposing.</param>
    /// <returns>A readable decompression stream.</returns>
    Stream CreateDecompressionStream(
        Stream inputStream,
        bool leaveOpen = false);
}
