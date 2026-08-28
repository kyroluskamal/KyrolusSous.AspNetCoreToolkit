using System.IO.Compression;
using KyrolusSous.RabbitMQ.Abstractions.Compression;

namespace KyrolusSous.RabbitMQ.Runtime.Compression;

/// <summary>
/// Gzip implementation of <see cref="IKyrolusMessageCompressor"/>.
/// </summary>
public class KyrolusGzipMessageCompressor : IKyrolusMessageCompressor
{
    public string EncodingName => "gzip";

    public byte[] Compress(byte[] rawBytes)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);

        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzipStream.Write(rawBytes, 0, rawBytes.Length);
        }

        return outputStream.ToArray();
    }

    public byte[] Decompress(byte[] compressedBytes)
    {
        ArgumentNullException.ThrowIfNull(compressedBytes);

        using var inputStream = new MemoryStream(compressedBytes);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();

        gzipStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }
}
