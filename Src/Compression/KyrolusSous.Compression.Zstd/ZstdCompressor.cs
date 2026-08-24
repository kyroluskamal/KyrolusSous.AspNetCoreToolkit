using ZstdSharp;

namespace KyrolusSous.Compression;

/// <summary>
/// High-performance compressor utilizing Meta's Zstandard (Zstd) algorithm.
/// </summary>
public sealed class ZstdCompressor : ICompressor
{
    public static ZstdCompressor Instance { get; } = new();

    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Zstd;

    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return [];
        }

        using var compressor = new Compressor(3);
        return compressor.Wrap(data).ToArray();
    }

    public byte[] Decompress(ReadOnlySpan<byte> compressedData)
    {
        if (compressedData.IsEmpty)
        {
            return [];
        }

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

        await using var zstdStream = new CompressionStream(destination, MapLevel(level), leaveOpen: true);
        await source.CopyToAsync(zstdStream, cancellationToken).ConfigureAwait(false);
        await zstdStream.FlushAsync(cancellationToken).ConfigureAwait(false);
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

    public Stream CreateCompressionStream(
        Stream outputStream,
        CompressionLevel level = CompressionLevel.Fastest,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        return new CompressionStream(outputStream, MapLevel(level), leaveOpen: leaveOpen);
    }

    public Stream CreateDecompressionStream(
        Stream inputStream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        return new DecompressionStream(inputStream, leaveOpen: leaveOpen);
    }

    private static int MapLevel(CompressionLevel level) => level switch
    {
        CompressionLevel.NoCompression => 1,
        CompressionLevel.Fastest => 1,
        CompressionLevel.Optimal => 3,
        CompressionLevel.SmallestSize => 19,
        _ => 3
    };
}
