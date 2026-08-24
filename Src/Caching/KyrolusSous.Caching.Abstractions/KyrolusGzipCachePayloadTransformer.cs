namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// A payload transformer utilizing the standard Gzip algorithm based on .NET's native <see cref="GZipStream"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Payload Framing Format:</b>
/// <c>[4-byte Magic Header 'KYC0'][1-byte Flag (0=Raw, 1=Compressed)][Gzip Compressed Body]</c>
/// </para>
/// </remarks>
/// <param name="minSizeBytes">Minimum byte length required to trigger compression. Defaults to 1024 bytes (1 KB).</param>
/// <param name="level">The compression quality/speed tradeoff level. Defaults to <see cref="CompressionLevel.Fastest"/>.</param>
public sealed class KyrolusGzipCachePayloadTransformer(
    int minSizeBytes = 1024,
    CompressionLevel level = CompressionLevel.Fastest) : IKyrolusCachePayloadTransformer
{
    private const byte RawFlag = 0;
    private const byte CompressedFlag = 1;
    private static readonly byte[] Header = [(byte)'K', (byte)'Y', (byte)'C', (byte)'0'];

    private readonly int minSizeBytes = Math.Max(0, minSizeBytes);
    private readonly CompressionLevel level = level;

    /// <summary>
    /// Compresses the serialized payload using Gzip if the size meets or exceeds <c>minSizeBytes</c>.
    /// </summary>
    /// <param name="payload">The original serialized byte array.</param>
    /// <returns>A framed byte array containing the 'KYC0' header and compressed data.</returns>
    public byte[] Transform(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Length < minSizeBytes)
        {
            return BuildRawPayload(payload);
        }

        using var output = new MemoryStream();
        output.Write(Header, 0, Header.Length);
        output.WriteByte(CompressedFlag);
        using (var gzip = new GZipStream(output, level, leaveOpen: true))
        {
            gzip.Write(payload, 0, payload.Length);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Decompresses the Gzip-compressed byte payload.
    /// </summary>
    /// <param name="payload">The framed byte array read from cache.</param>
    /// <returns>The uncompressed original serialized byte array.</returns>
    public byte[] Restore(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!HasHeader(payload))
        {
            return payload;
        }

        var flag = payload[Header.Length];
        if (flag == RawFlag)
        {
            return SlicePayload(payload, Header.Length + 1);
        }

        if (flag != CompressedFlag)
        {
            return payload;
        }

        using var input = new MemoryStream(payload, Header.Length + 1, payload.Length - Header.Length - 1);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static bool HasHeader(byte[] payload)
    {
        if (payload.Length <= Header.Length)
        {
            return false;
        }

        for (var i = 0; i < Header.Length; i++)
        {
            if (payload[i] != Header[i])
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] BuildRawPayload(byte[] payload)
    {
        var result = new byte[Header.Length + 1 + payload.Length];
        Buffer.BlockCopy(Header, 0, result, 0, Header.Length);
        result[Header.Length] = RawFlag;
        Buffer.BlockCopy(payload, 0, result, Header.Length + 1, payload.Length);
        return result;
    }

    private static byte[] SlicePayload(byte[] payload, int offset)
    {
        if (offset >= payload.Length)
        {
            return [];
        }

        var result = new byte[payload.Length - offset];
        Buffer.BlockCopy(payload, offset, result, 0, result.Length);
        return result;
    }
}
