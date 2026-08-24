using System.Buffers.Binary;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace KyrolusSous.Compression;

/// <summary>
/// Ultra-fast compressor utilizing the LZ4 algorithm for high-throughput real-time systems.
/// </summary>
public sealed class Lz4Compressor : ICompressor
{
    public static Lz4Compressor Instance { get; } = new();

    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Lz4;

    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return [];
        }

        var maxCompressedLength = LZ4Codec.MaximumOutputSize(data.Length);
        var output = new byte[maxCompressedLength + 4]; // Prepend original length (4 bytes)

        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(0, 4), data.Length);
        var encodedBytes = LZ4Codec.Encode(
            data,
            output.AsSpan(4),
            LZ4Level.L00_FAST);

        var result = new byte[encodedBytes + 4];
        output.AsSpan(0, encodedBytes + 4).CopyTo(result);
        return result;
    }

    public byte[] Decompress(ReadOnlySpan<byte> compressedData)
    {
        if (compressedData.IsEmpty)
        {
            return [];
        }

        if (compressedData.Length < 4)
        {
            throw new InvalidOperationException("Invalid LZ4 compressed payload: header missing.");
        }

        var originalLength = BinaryPrimitives.ReadInt32LittleEndian(compressedData.Slice(0, 4));
        if (originalLength < 0)
        {
            throw new InvalidOperationException("Invalid LZ4 compressed payload: negative length header.");
        }

        if (originalLength == 0)
        {
            return [];
        }

        var output = new byte[originalLength];

        var decodedBytes = LZ4Codec.Decode(
            compressedData.Slice(4),
            output.AsSpan());

        if (decodedBytes < 0 || decodedBytes != originalLength)
        {
            throw new InvalidOperationException("LZ4 decompression failed: payload corrupted or size mismatch.");
        }

        return output;
    }

    public async Task CompressAsync(
        Stream source,
        Stream destination,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        await using var lz4Stream = LZ4Stream.Encode(destination, MapLevel(level), leaveOpen: true);
        await source.CopyToAsync(lz4Stream, cancellationToken).ConfigureAwait(false);
        await lz4Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        await using var lz4Stream = LZ4Stream.Decode(source, leaveOpen: true);
        await lz4Stream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    public Stream CreateCompressionStream(
        Stream outputStream,
        CompressionLevel level = CompressionLevel.Fastest,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        return LZ4Stream.Encode(outputStream, MapLevel(level), leaveOpen: leaveOpen);
    }

    public Stream CreateDecompressionStream(
        Stream inputStream,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        return LZ4Stream.Decode(inputStream, leaveOpen: leaveOpen);
    }

    private static LZ4Level MapLevel(CompressionLevel level) => level switch
    {
        CompressionLevel.NoCompression => LZ4Level.L00_FAST,
        CompressionLevel.Fastest => LZ4Level.L00_FAST,
        CompressionLevel.Optimal => LZ4Level.L09_HC,
        CompressionLevel.SmallestSize => LZ4Level.L12_MAX,
        _ => LZ4Level.L00_FAST
    };
}
