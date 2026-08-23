using System.IO.Compression;
using System.Text;
using ZstdSharp;

namespace KyrolusSous.Compression;

/// <summary>
/// High-performance compressor utilizing Meta's Zstandard (Zstd) algorithm.
/// </summary>
public sealed class ZstdCompressor : ICompressor
{
    public static ZstdCompressor Instance { get; } = new();

    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Zstd;

    public byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Fastest)
    {
        if (data is null || data.Length == 0) return [];
        int zstdLevel = MapCompressionLevel(level);
        using var compressor = new Compressor(zstdLevel);
        return compressor.Wrap(data).ToArray();
    }

    public byte[] Decompress(byte[] compressedData)
    {
        if (compressedData is null || compressedData.Length == 0) return [];
        using var decompressor = new Decompressor();
        return decompressor.Unwrap(compressedData).ToArray();
    }

    public async Task CompressAsync(
        Stream source,
        Stream destination,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        int zstdLevel = MapCompressionLevel(level);
        await using var zstdStream = new CompressionStream(destination, zstdLevel, leaveOpen: true);
        await source.CopyToAsync(zstdStream, cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var zstdStream = new DecompressionStream(source, leaveOpen: true);
        await zstdStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
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

    private static int MapCompressionLevel(CompressionLevel level) => level switch
    {
        CompressionLevel.NoCompression => 1,
        CompressionLevel.Fastest => 1,
        CompressionLevel.Optimal => 3,
        CompressionLevel.SmallestSize => 19,
        _ => 3
    };
}
