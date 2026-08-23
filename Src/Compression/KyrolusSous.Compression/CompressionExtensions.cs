using System.IO.Compression;

namespace KyrolusSous.Compression;

/// <summary>
/// Convenient extension methods for compressing and decompressing byte arrays, strings, and streams.
/// </summary>
public static class CompressionExtensions
{
    /// <summary>
    /// Compresses a byte array using the specified algorithm (defaults to Brotli).
    /// </summary>
    public static byte[] Compress(
        this byte[] data,
        CompressionAlgorithm algorithm = CompressionAlgorithm.Brotli,
        CompressionLevel level = CompressionLevel.Fastest)
    {
        var compressor = KyrolusCompressionProvider.Instance.GetCompressor(algorithm);
        return compressor.Compress(data, level);
    }

    /// <summary>
    /// Decompresses a byte array using the specified algorithm (defaults to Brotli).
    /// </summary>
    public static byte[] Decompress(
        this byte[] compressedData,
        CompressionAlgorithm algorithm = CompressionAlgorithm.Brotli)
    {
        var compressor = KyrolusCompressionProvider.Instance.GetCompressor(algorithm);
        return compressor.Decompress(compressedData);
    }

    /// <summary>
    /// Compresses a byte array using Google's Brotli algorithm.
    /// </summary>
    public static byte[] CompressWithBrotli(this byte[] data, CompressionLevel level = CompressionLevel.Fastest) =>
        BrotliCompressor.Instance.Compress(data, level);

    /// <summary>
    /// Decompresses a Brotli-compressed byte array.
    /// </summary>
    public static byte[] DecompressBrotli(this byte[] compressedData) =>
        BrotliCompressor.Instance.Decompress(compressedData);

    /// <summary>
    /// Compresses a byte array using Meta's Zstandard (Zstd) algorithm.
    /// </summary>
    public static byte[] CompressWithZstd(this byte[] data, CompressionLevel level = CompressionLevel.Fastest) =>
        ZstdCompressor.Instance.Compress(data, level);

    /// <summary>
    /// Decompresses a Zstandard-compressed byte array.
    /// </summary>
    public static byte[] DecompressZstd(this byte[] compressedData) =>
        ZstdCompressor.Instance.Decompress(compressedData);

    /// <summary>
    /// Compresses a byte array using the ultra-fast LZ4 algorithm.
    /// </summary>
    public static byte[] CompressWithLz4(this byte[] data, CompressionLevel level = CompressionLevel.Fastest) =>
        Lz4Compressor.Instance.Compress(data, level);

    /// <summary>
    /// Decompresses an LZ4-compressed byte array.
    /// </summary>
    public static byte[] DecompressLz4(this byte[] compressedData) =>
        Lz4Compressor.Instance.Decompress(compressedData);

    /// <summary>
    /// Compresses a byte array using Google's Snappy algorithm.
    /// </summary>
    public static byte[] CompressWithSnappy(this byte[] data, CompressionLevel level = CompressionLevel.Fastest) =>
        SnappyCompressor.Instance.Compress(data, level);

    /// <summary>
    /// Decompresses a Snappy-compressed byte array.
    /// </summary>
    public static byte[] DecompressSnappy(this byte[] compressedData) =>
        SnappyCompressor.Instance.Decompress(compressedData);

    /// <summary>
    /// Compresses a byte array using standard Gzip.
    /// </summary>
    public static byte[] CompressWithGzip(this byte[] data, CompressionLevel level = CompressionLevel.Fastest) =>
        GzipCompressor.Instance.Compress(data, level);

    /// <summary>
    /// Decompresses a Gzip-compressed byte array.
    /// </summary>
    public static byte[] DecompressGzip(this byte[] compressedData) =>
        GzipCompressor.Instance.Decompress(compressedData);

    /// <summary>
    /// Compresses a string into a Base64-encoded compressed string using the specified algorithm (defaults to Brotli).
    /// </summary>
    public static string CompressString(
        this string text,
        CompressionAlgorithm algorithm = CompressionAlgorithm.Brotli,
        CompressionLevel level = CompressionLevel.Fastest)
    {
        var compressor = KyrolusCompressionProvider.Instance.GetCompressor(algorithm);
        return compressor.CompressString(text, level);
    }

    /// <summary>
    /// Decompresses a Base64-encoded compressed string back to UTF-8 text using the specified algorithm (defaults to Brotli).
    /// </summary>
    public static string DecompressString(
        this string compressedBase64,
        CompressionAlgorithm algorithm = CompressionAlgorithm.Brotli)
    {
        var compressor = KyrolusCompressionProvider.Instance.GetCompressor(algorithm);
        return compressor.DecompressString(compressedBase64);
    }
}
