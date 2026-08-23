using System.IO.Compression;
using System.Text;

namespace KyrolusSous.Compression;

/// <summary>
/// Universal compressor utilizing standard Gzip algorithm.
/// </summary>
public sealed class GzipCompressor : ICompressor
{
    public static GzipCompressor Instance { get; } = new();

    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Gzip;

    public byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Fastest)
    {
        if (data is null || data.Length == 0) return [];

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, level, leaveOpen: true))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    public byte[] Decompress(byte[] compressedData)
    {
        if (compressedData is null || compressedData.Length == 0) return [];

        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
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

        await using var gzip = new GZipStream(destination, level, leaveOpen: true);
        await source.CopyToAsync(gzip, cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        await gzip.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
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
