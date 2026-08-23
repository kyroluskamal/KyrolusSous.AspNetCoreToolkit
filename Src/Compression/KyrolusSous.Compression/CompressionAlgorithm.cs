namespace KyrolusSous.Compression;

/// <summary>
/// Specifies the compression algorithm.
/// </summary>
public enum CompressionAlgorithm
{
    /// <summary>
    /// Google's Brotli algorithm. Provides the highest compression ratio (up to 25% better than Gzip).
    /// </summary>
    Brotli = 0,

    /// <summary>
    /// Meta/Facebook's Zstandard algorithm. Best-in-class balance between high compression ratio and ultra-fast decompression.
    /// </summary>
    Zstd = 1,

    /// <summary>
    /// LZ4 algorithm. The fastest real-time compression algorithm in the world with low CPU overhead.
    /// </summary>
    Lz4 = 2,

    /// <summary>
    /// Google's Snappy algorithm. Fast block compression optimized for streaming and databases.
    /// </summary>
    Snappy = 3,

    /// <summary>
    /// Standard Gzip compression. Universally compatible across all platforms.
    /// </summary>
    Gzip = 4,

    /// <summary>
    /// Raw Deflate compression without Gzip container overhead.
    /// </summary>
    Deflate = 5
}
