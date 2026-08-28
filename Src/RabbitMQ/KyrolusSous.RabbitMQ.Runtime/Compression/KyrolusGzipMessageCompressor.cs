using System.IO.Compression;
using KyrolusSous.RabbitMQ.Abstractions.Compression;

namespace KyrolusSous.RabbitMQ.Runtime.Compression;

/// <summary>
/// Gzip implementation of <see cref="IKyrolusMessageCompressor"/> with decompression bomb protection.
/// </summary>
public class KyrolusGzipMessageCompressor : IKyrolusMessageCompressor
{
    private readonly long _maxDecompressedBytes;

    public string EncodingName => "gzip";

    public KyrolusGzipMessageCompressor(long maxDecompressedBytes = 50 * 1024 * 1024) // 50 MB default
    {
        _maxDecompressedBytes = Math.Max(1024, maxDecompressedBytes);
    }

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

        byte[] buffer = new byte[81920];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = gzipStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytesRead += bytesRead;
            if (totalBytesRead > _maxDecompressedBytes)
            {
                throw new InvalidOperationException($"Decompressed payload exceeds maximum allowable size of {_maxDecompressedBytes} bytes (Decompression bomb protection).");
            }

            outputStream.Write(buffer, 0, bytesRead);
        }

        return outputStream.ToArray();
    }
}
