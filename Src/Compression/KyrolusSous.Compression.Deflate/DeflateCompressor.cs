namespace KyrolusSous.Compression;

/// <summary>
/// Lightweight compressor utilizing raw Deflate algorithm.
/// </summary>
public sealed class DeflateCompressor : IKyrolusCompressor
{
    public static DeflateCompressor Instance { get; } = new();

    public KyrolusCompressionAlgorithm Algorithm => KyrolusCompressionAlgorithm.Deflate;

    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return [];
        }

        using var memoryStream = new MemoryStream();
        using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflateStream.Write(data);
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
        using var deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        deflateStream.CopyTo(outputStream);
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

        await using var deflateStream = new DeflateStream(destination, level, leaveOpen: true);
        await source.CopyToAsync(deflateStream, cancellationToken).ConfigureAwait(false);
        await deflateStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var deflateStream = new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true);
        await deflateStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    public Stream CreateCompressionStream(
        Stream outputStream,
        CompressionLevel level = CompressionLevel.Fastest,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        return new DeflateStream(outputStream, level, leaveOpen);
    }

    public Stream CreateDecompressionStream(
        Stream inputStream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        return new DeflateStream(inputStream, CompressionMode.Decompress, leaveOpen);
    }
}
