using System.IO.Compression;
using System.Text;

namespace KyrolusSous.Compression;

/// <summary>
/// High-efficiency compressor utilizing Google's Brotli algorithm.
/// </summary>
public sealed class BrotliCompressor : ICompressor
{
    public static BrotliCompressor Instance { get; } = new();

    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Brotli;

    public byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Fastest)
    {
        if (data is null || data.Length == 0) return [];

        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, level, leaveOpen: true))
        {
            brotli.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    public byte[] Decompress(byte[] compressedData)
    {
        if (compressedData is null || compressedData.Length == 0) return [];

        using var input = new MemoryStream(compressedData);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

    public async Task CompressAsync(
        Stream source,
        Stream destination,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var brotli = new BrotliStream(destination, level, leaveOpen: true);
        await source.CopyToAsync(brotli, cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var brotli = new BrotliStream(source, CompressionMode.Decompress, leaveOpen: true);
        await brotli.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
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
