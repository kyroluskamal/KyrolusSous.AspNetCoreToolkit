namespace KyrolusSous.Compression;

/// <summary>
/// Universal compressor utilizing standard Gzip algorithm.
/// </summary>
public sealed class GzipCompressor : IKyrolusCompressor
{
    public static GzipCompressor Instance { get; } = new();

    public KyrolusCompressionAlgorithm Algorithm => KyrolusCompressionAlgorithm.Gzip;

    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return [];
        }

        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzipStream.Write(data);
        }

        return memoryStream.ToArray();
    }

    public byte[] Decompress(ReadOnlySpan<byte> compressedData)
    {
        if (compressedData.IsEmpty)
        {
            return [];
        }

        using var memoryStream = new MemoryStream(compressedData.ToArray());
        using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    public async Task CompressAsync(
        Stream source,
        Stream destination,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var gzipStream = new GZipStream(destination, level, leaveOpen: true);
        await source.CopyToAsync(gzipStream, cancellationToken).ConfigureAwait(false);
        await gzipStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var gzipStream = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        await gzipStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    public Stream CreateCompressionStream(
        Stream outputStream,
        CompressionLevel level = CompressionLevel.Fastest,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        return new GZipStream(outputStream, level, leaveOpen);
    }

    public Stream CreateDecompressionStream(
        Stream inputStream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        return new GZipStream(inputStream, CompressionMode.Decompress, leaveOpen);
    }
}
