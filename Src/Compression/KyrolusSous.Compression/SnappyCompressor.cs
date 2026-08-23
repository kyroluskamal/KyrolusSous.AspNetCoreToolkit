using System.IO.Compression;
using System.Text;
using Snappier;

namespace KyrolusSous.Compression;

/// <summary>
/// Fast compressor utilizing Google's Snappy algorithm.
/// </summary>
public sealed class SnappyCompressor : ICompressor
{
    public static SnappyCompressor Instance { get; } = new();

    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Snappy;

    public byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Fastest)
    {
        if (data is null || data.Length == 0) return [];
        return Snappy.CompressToArray(data);
    }

    public byte[] Decompress(byte[] compressedData)
    {
        if (compressedData is null || compressedData.Length == 0) return [];
        return Snappy.DecompressToArray(compressedData);
    }

    public async Task CompressAsync(
        Stream source,
        Stream destination,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var snappyStream = new SnappyStream(destination, CompressionMode.Compress, leaveOpen: true);
        await source.CopyToAsync(snappyStream, cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var snappyStream = new SnappyStream(source, CompressionMode.Decompress, leaveOpen: true);
        await snappyStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    public string CompressString(string text, CompressionLevel level = CompressionLevel.Fastest)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(text);
        var compressed = Compress(bytes, level);
        return Convert.ToBase64String(compressed);
    }

    public string DecompressString(string compressedBase64)
    {
        if (string.IsNullOrEmpty(compressedBase64)) return string.Empty;
        var bytes = Convert.FromBase64String(compressedBase64);
        var decompressed = Decompress(bytes);
        return Encoding.UTF8.GetString(decompressed);
    }
}
