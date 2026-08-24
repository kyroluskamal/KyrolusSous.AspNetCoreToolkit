namespace KyrolusSous.Compression;

/// <summary>
/// Supported compression algorithms across the Kyrolus compression ecosystem.
/// </summary>
public enum CompressionAlgorithm
{
    /// <summary>
    /// Google's Brotli compression algorithm. High compression ratio, ideal for web payloads.
    /// </summary>
    Brotli = 0,

    /// <summary>
    /// Meta's Zstandard algorithm. High speed and tunable compression ratios.
    /// </summary>
    Zstd = 1,

    /// <summary>
    /// LZ4 lossless compression algorithm. Extreme decompression speed (multi-GB/s).
    /// </summary>
    Lz4 = 2,

    /// <summary>
    /// Google's Snappy compression algorithm. Optimized for very high throughput.
    /// </summary>
    Snappy = 3,

    /// <summary>
    /// Standard Gzip compression algorithm. Maximum compatibility.
    /// </summary>
    Gzip = 4,

    /// <summary>
    /// Raw Deflate compression algorithm without gzip headers.
    /// </summary>
    Deflate = 5
}
