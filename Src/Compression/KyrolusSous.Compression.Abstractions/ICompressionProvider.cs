namespace KyrolusSous.Compression;

/// <summary>
/// Provides access to compressors by their algorithm type.
/// </summary>
public interface IKyrolusCompressionProvider
{
    /// <summary>
    /// Gets the compressor for the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The desired compression algorithm.</param>
    /// <returns>An <see cref="IKyrolusCompressor"/> instance for the requested algorithm.</returns>
    IKyrolusCompressor GetCompressor(KyrolusCompressionAlgorithm algorithm);

    /// <summary>
    /// Tries to get the compressor for the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The desired compression algorithm.</param>
    /// <param name="compressor">When this method returns, contains the compressor associated with the specified algorithm, if found.</param>
    /// <returns><see langword="true"/> if the compressor was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetCompressor(KyrolusCompressionAlgorithm algorithm, out IKyrolusCompressor? compressor);

    /// <summary>
    /// Gets the default compressor (defaults to Brotli).
    /// </summary>
    IKyrolusCompressor DefaultCompressor { get; }
}
