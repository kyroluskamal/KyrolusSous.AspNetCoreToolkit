using System.Collections.Concurrent;

namespace KyrolusSous.Compression;

/// <summary>
/// Thread-safe central registry and provider for all registered compression algorithms.
/// </summary>
public sealed class KyrolusCompressionProvider : ICompressionProvider
{
    private readonly ConcurrentDictionary<CompressionAlgorithm, ICompressor> _compressors = new();

    /// <summary>
    /// Singleton instance of the provider registry.
    /// </summary>
    public static KyrolusCompressionProvider Instance { get; } = new();

    /// <summary>
    /// Registers a compressor implementation into the provider registry.
    /// </summary>
    /// <param name="compressor">The compressor instance to register.</param>
    public void Register(ICompressor compressor)
    {
        ArgumentNullException.ThrowIfNull(compressor);
        _compressors[compressor.Algorithm] = compressor;
    }

    /// <summary>
    /// Gets the compressor for the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The requested compression algorithm.</param>
    /// <returns>The registered <see cref="ICompressor"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown when the requested algorithm package is not registered.</exception>
    public ICompressor GetCompressor(CompressionAlgorithm algorithm)
    {
        if (_compressors.TryGetValue(algorithm, out var compressor))
        {
            return compressor;
        }

        throw new NotSupportedException(
            $"Compression algorithm '{algorithm}' is not registered. " +
            $"Please ensure the corresponding package is installed and registered (e.g. AddKyrolus{algorithm}Compression()).");
    }

    /// <summary>
    /// Tries to get the compressor for the specified algorithm.
    /// </summary>
    public bool TryGetCompressor(CompressionAlgorithm algorithm, out ICompressor? compressor) =>
        _compressors.TryGetValue(algorithm, out compressor);

    /// <summary>
    /// Gets the default compressor (Brotli if registered, or the first registered compressor).
    /// </summary>
    public ICompressor DefaultCompressor =>
        _compressors.TryGetValue(CompressionAlgorithm.Brotli, out var brotli)
            ? brotli
            : _compressors.Values.FirstOrDefault()
              ?? throw new InvalidOperationException("No compression algorithms have been registered in KyrolusCompressionProvider.");
}
