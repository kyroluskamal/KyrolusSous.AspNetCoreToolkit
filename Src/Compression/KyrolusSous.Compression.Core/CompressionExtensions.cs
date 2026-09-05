namespace KyrolusSous.Compression;

/// <summary>
/// Convenient extension methods for compressing and decompressing byte arrays, strings, and streams.
/// </summary>
public static class CompressionExtensions
{
    /// <summary>
    /// Compresses a byte array using the specified algorithm (defaults to Brotli).
    /// </summary>
    public static byte[] Compress(this byte[] data, KyrolusCompressionAlgorithm algorithm = KyrolusCompressionAlgorithm.Brotli) =>
        KyrolusCompressionProvider.Instance.GetCompressor(algorithm).Compress(data);

    /// <summary>
    /// Compresses a read-only byte span using the specified algorithm (defaults to Brotli).
    /// </summary>
    public static byte[] Compress(this ReadOnlySpan<byte> data, KyrolusCompressionAlgorithm algorithm = KyrolusCompressionAlgorithm.Brotli) =>
        KyrolusCompressionProvider.Instance.GetCompressor(algorithm).Compress(data);

    /// <summary>
    /// Decompresses a compressed byte array using the specified algorithm (defaults to Brotli).
    /// </summary>
    public static byte[] Decompress(this byte[] compressedData, KyrolusCompressionAlgorithm algorithm = KyrolusCompressionAlgorithm.Brotli) =>
        KyrolusCompressionProvider.Instance.GetCompressor(algorithm).Decompress(compressedData);

    /// <summary>
    /// Decompresses a compressed read-only byte span using the specified algorithm (defaults to Brotli).
    /// </summary>
    public static byte[] Decompress(this ReadOnlySpan<byte> compressedData, KyrolusCompressionAlgorithm algorithm = KyrolusCompressionAlgorithm.Brotli) =>
        KyrolusCompressionProvider.Instance.GetCompressor(algorithm).Decompress(compressedData);

    /// <summary>
    /// Compresses a UTF-8 string into a Base64-encoded compressed string.
    /// </summary>
    public static string CompressString(this string text, KyrolusCompressionAlgorithm algorithm = KyrolusCompressionAlgorithm.Brotli)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        var compressed = KyrolusCompressionProvider.Instance.GetCompressor(algorithm).Compress(bytes);
        return Convert.ToBase64String(compressed);
    }

    /// <summary>
    /// Decompresses a Base64-encoded compressed string back to the original UTF-8 string.
    /// </summary>
    public static string DecompressString(this string base64Compressed, KyrolusCompressionAlgorithm algorithm = KyrolusCompressionAlgorithm.Brotli)
    {
        ArgumentNullException.ThrowIfNull(base64Compressed);
        var compressedBytes = Convert.FromBase64String(base64Compressed);
        var decompressedBytes = KyrolusCompressionProvider.Instance.GetCompressor(algorithm).Decompress(compressedBytes);
        return Encoding.UTF8.GetString(decompressedBytes);
    }

    /// <summary>
    /// Asynchronously compresses a source stream into a destination stream.
    /// </summary>
    public static Task CompressToStreamAsync(
        this Stream source,
        Stream destination,
        KyrolusCompressionAlgorithm algorithm = KyrolusCompressionAlgorithm.Brotli,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default) =>
        KyrolusCompressionProvider.Instance.GetCompressor(algorithm).CompressAsync(source, destination, level, cancellationToken);

    /// <summary>
    /// Asynchronously decompresses a source stream into a destination stream.
    /// </summary>
    public static Task DecompressToStreamAsync(
        this Stream source,
        Stream destination,
        KyrolusCompressionAlgorithm algorithm = KyrolusCompressionAlgorithm.Brotli,
        CancellationToken cancellationToken = default) =>
        KyrolusCompressionProvider.Instance.GetCompressor(algorithm).DecompressAsync(source, destination, cancellationToken);
}
