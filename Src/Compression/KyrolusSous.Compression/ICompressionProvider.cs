namespace KyrolusSous.Compression;

/// <summary>
/// Provider for resolving <see cref="ICompressor"/> instances by algorithm.
/// </summary>
public interface ICompressionProvider
{
    /// <summary>
    /// Gets the compressor for the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The compression algorithm.</param>
    /// <returns>An instance of <see cref="ICompressor"/>.</returns>
    ICompressor GetCompressor(CompressionAlgorithm algorithm);

    /// <summary>
    /// Gets the default compressor (Brotli).
    /// </summary>
    ICompressor DefaultCompressor { get; }
}

/// <summary>
/// Default implementation of <see cref="ICompressionProvider"/>.
/// </summary>
public sealed class KyrolusCompressionProvider : ICompressionProvider
{
    public static KyrolusCompressionProvider Instance { get; } = new();

    public ICompressor DefaultCompressor => BrotliCompressor.Instance;

    public ICompressor GetCompressor(CompressionAlgorithm algorithm) => algorithm switch
    {
        CompressionAlgorithm.Brotli => BrotliCompressor.Instance,
        CompressionAlgorithm.Zstd => ZstdCompressor.Instance,
        CompressionAlgorithm.Lz4 => Lz4Compressor.Instance,
        CompressionAlgorithm.Snappy => SnappyCompressor.Instance,
        CompressionAlgorithm.Gzip => GzipCompressor.Instance,
        CompressionAlgorithm.Deflate => DeflateCompressor.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported compression algorithm.")
    };
}
