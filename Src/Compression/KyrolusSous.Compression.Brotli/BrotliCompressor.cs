namespace KyrolusSous.Compression;

/// <summary>
/// High-efficiency compressor utilizing Google's Brotli algorithm.
/// </summary>
public sealed class BrotliCompressor : ICompressor
{
    public static BrotliCompressor Instance { get; } = new();

    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Brotli;

    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return [];
        }

        using var memoryStream = new MemoryStream();
        using (var brotliStream = new BrotliStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotliStream.Write(data);
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
        using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        brotliStream.CopyTo(outputStream);
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

        await using var brotliStream = new BrotliStream(destination, level, leaveOpen: true);
        await source.CopyToAsync(brotliStream, cancellationToken).ConfigureAwait(false);
        await brotliStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var brotliStream = new BrotliStream(source, CompressionMode.Decompress, leaveOpen: true);
        await brotliStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    public Stream CreateCompressionStream(
        Stream outputStream,
        CompressionLevel level = CompressionLevel.Fastest,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        return new BrotliStream(outputStream, level, leaveOpen);
    }

    public Stream CreateDecompressionStream(
        Stream inputStream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        return new BrotliStream(inputStream, CompressionMode.Decompress, leaveOpen);
    }
}
