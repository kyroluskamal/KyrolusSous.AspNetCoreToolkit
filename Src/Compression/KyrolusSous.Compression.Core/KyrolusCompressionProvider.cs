using System.Collections.Concurrent;

namespace KyrolusSous.Compression;

/// <summary>
/// Thread-safe central registry and provider for all registered compression algorithms.
/// </summary>
public sealed class KyrolusCompressionProvider : IKyrolusCompressionProvider
{
    private readonly ConcurrentDictionary<KyrolusCompressionAlgorithm, IKyrolusCompressor> _compressors = new();

    /// <summary>
    /// Singleton instance of the provider registry.
    /// </summary>
    public static KyrolusCompressionProvider Instance { get; } = new();

    /// <summary>
    /// Registers a compressor implementation into the provider registry.
    /// </summary>
    /// <param name="compressor">The compressor instance to register.</param>
    public void Register(IKyrolusCompressor compressor)
    {
        ArgumentNullException.ThrowIfNull(compressor);
        _compressors[compressor.Algorithm] = compressor;
    }

    /// <summary>
    /// Gets the compressor for the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The requested compression algorithm.</param>
    /// <returns>The registered <see cref="IKyrolusCompressor"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown when the requested algorithm package is not registered.</exception>
    public IKyrolusCompressor GetCompressor(KyrolusCompressionAlgorithm algorithm)
    {
        if (_compressors.TryGetValue(algorithm, out var compressor))
        {
            return compressor;
        }

        throw new NotSupportedException(
            $"Compression algorithm '{algorithm}' is not registered. " +
            $"Please ensure the corresponding package is installed and registered (e.g. AddKyrolus{algorithm}Compression()).");
    }

    IKyrolusCompressor IKyrolusCompressionProvider.GetCompressor(KyrolusCompressionAlgorithm algorithm) =>
        (IKyrolusCompressor)GetCompressor(algorithm);

    /// <summary>
    /// Tries to get the compressor for the specified algorithm.
    /// </summary>
    public bool TryGetCompressor(KyrolusCompressionAlgorithm algorithm, out IKyrolusCompressor? compressor) =>
        _compressors.TryGetValue(algorithm, out compressor);

    bool IKyrolusCompressionProvider.TryGetCompressor(KyrolusCompressionAlgorithm algorithm, out IKyrolusCompressor? compressor)
    {
        if (_compressors.TryGetValue(algorithm, out var comp) && comp is IKyrolusCompressor c)
        {
            compressor = c;
            return true;
        }

        compressor = null;
        return false;
    }

    /// <summary>
    /// Gets the default compressor (Brotli if registered, or the first registered compressor).
    /// </summary>
    public IKyrolusCompressor DefaultCompressor =>
        _compressors.TryGetValue(KyrolusCompressionAlgorithm.Brotli, out var brotli)
            ? brotli
            : _compressors.Values.FirstOrDefault()
              ?? throw new InvalidOperationException("No compression algorithms have been registered in KyrolusCompressionProvider.");

    IKyrolusCompressor IKyrolusCompressionProvider.DefaultCompressor =>
        (IKyrolusCompressor)DefaultCompressor;
}
