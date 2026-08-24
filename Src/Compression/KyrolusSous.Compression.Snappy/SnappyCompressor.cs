using Snappier;

namespace KyrolusSous.Compression;

/// <summary>
/// Fast compressor utilizing Google's Snappy algorithm.
/// </summary>
public sealed class SnappyCompressor : ICompressor
{
    public static SnappyCompressor Instance { get; } = new();

    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Snappy;

    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return [];
        }

        return Snappy.CompressToArray(data);
    }

    public byte[] Decompress(ReadOnlySpan<byte> compressedData)
    {
        if (compressedData.IsEmpty)
        {
            return [];
        }

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
        await snappyStream.FlushAsync(cancellationToken).ConfigureAwait(false);
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

    public Stream CreateCompressionStream(
        Stream outputStream,
        CompressionLevel level = CompressionLevel.Fastest,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        return new SnappyStream(outputStream, CompressionMode.Compress, leaveOpen: leaveOpen);
    }

    public Stream CreateDecompressionStream(
        Stream inputStream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        return new SnappyStream(inputStream, CompressionMode.Decompress, leaveOpen: leaveOpen);
    }
}
