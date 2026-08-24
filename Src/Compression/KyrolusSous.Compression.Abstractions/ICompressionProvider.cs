namespace KyrolusSous.Compression;

/// <summary>
/// Provides access to compressors by their algorithm type.
/// </summary>
public interface ICompressionProvider
{
    /// <summary>
    /// Gets the compressor for the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The desired compression algorithm.</param>
    /// <returns>An <see cref="ICompressor"/> instance for the requested algorithm.</returns>
    ICompressor GetCompressor(CompressionAlgorithm algorithm);

    /// <summary>
    /// Tries to get the compressor for the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The desired compression algorithm.</param>
    /// <param name="compressor">When this method returns, contains the compressor associated with the specified algorithm, if found.</param>
    /// <returns><see langword="true"/> if the compressor was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetCompressor(CompressionAlgorithm algorithm, out ICompressor? compressor);

    /// <summary>
    /// Gets the default compressor (defaults to Brotli).
    /// </summary>
    ICompressor DefaultCompressor { get; }
}
