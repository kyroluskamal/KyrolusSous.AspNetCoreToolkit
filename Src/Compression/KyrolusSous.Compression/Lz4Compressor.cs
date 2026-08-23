using System.IO.Compression;
using System.Text;
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

    public byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Fastest)
    {
        if (data is null || data.Length == 0) return [];
        var lz4Level = MapCompressionLevel(level);
        var target = new byte[LZ4Codec.MaximumOutputSize(data.Length)];
        var encodedLength = LZ4Codec.Encode(data, 0, data.Length, target, 0, target.Length, lz4Level);
        if (encodedLength <= 0) return [];

        // Prepend original length (4 bytes) so decompression knows exact target buffer size
        var result = new byte[encodedLength + 4];
        BitConverter.TryWriteBytes(result.AsSpan(0, 4), data.Length);
        Buffer.BlockCopy(target, 0, result, 4, encodedLength);
        return result;
    }

    public byte[] Decompress(byte[] compressedData)
    {
        if (compressedData is null || compressedData.Length < 4) return [];

        int originalLength = BitConverter.ToInt32(compressedData, 0);
        if (originalLength <= 0) return [];

        var target = new byte[originalLength];
        var decodedLength = LZ4Codec.Decode(
            compressedData, 4, compressedData.Length - 4,
            target, 0, originalLength);

        return decodedLength == originalLength ? target : [];
    }

    public async Task CompressAsync(
        Stream source,
        Stream destination,
        CompressionLevel level = CompressionLevel.Fastest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var lz4Level = MapCompressionLevel(level);
        await using var lz4Stream = LZ4Stream.Encode(destination, new LZ4EncoderSettings { CompressionLevel = lz4Level }, leaveOpen: true);
        await source.CopyToAsync(lz4Stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var lz4Stream = LZ4Stream.Decode(source, leaveOpen: true);
        await lz4Stream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
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

    private static LZ4Level MapCompressionLevel(CompressionLevel level) => level switch
    {
        CompressionLevel.NoCompression => LZ4Level.L00_FAST,
        CompressionLevel.Fastest => LZ4Level.L00_FAST,
        CompressionLevel.Optimal => LZ4Level.L09_HC,
        CompressionLevel.SmallestSize => LZ4Level.L12_MAX,
        _ => LZ4Level.L00_FAST
    };
}
