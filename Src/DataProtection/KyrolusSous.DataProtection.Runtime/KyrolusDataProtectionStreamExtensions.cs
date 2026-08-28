using System.Buffers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.Runtime;

/// <summary>
/// Streaming encryption and decryption extensions for <see cref="IDataProtector"/>.
/// </summary>
public static class KyrolusDataProtectionStreamExtensions
{
    private static readonly byte[] StreamMagic = "KSDP"u8.ToArray();
    private const byte StreamVersion = 0x01;
    private const int DefaultChunkSize = 64 * 1024; // 64 KB
    private const int MaxAllowedChunkSize = 16 * 1024 * 1024; // 16 MB max safety limit

    /// <summary>
    /// Encrypts the input stream and writes framed ciphertext chunks to the output stream.
    /// </summary>
    /// <param name="protector">The data protector instance.</param>
    /// <param name="inputStream">The plaintext stream to read from.</param>
    /// <param name="outputStream">The encrypted stream to write to.</param>
    /// <param name="chunkSize">The buffer chunk size (default 64KB).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ProtectStreamAsync(
        this IDataProtector protector,
        Stream inputStream,
        Stream outputStream,
        int chunkSize = DefaultChunkSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(outputStream);

        if (chunkSize <= 0 || chunkSize > MaxAllowedChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be between 1 byte and 16 MB.");
        }

        // 1. Write magic and format version
        await outputStream.WriteAsync(StreamMagic, cancellationToken).ConfigureAwait(false);
        outputStream.WriteByte(StreamVersion);

        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        var lengthBuffer = new byte[4];

        try
        {
            int bytesRead;
            while ((bytesRead = await inputStream.ReadAsync(buffer.AsMemory(0, chunkSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                var slice = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, slice, 0, bytesRead);

                var encryptedBytes = protector.Protect(slice);

                BitConverter.TryWriteBytes(lengthBuffer, encryptedBytes.Length);
                await outputStream.WriteAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
                await outputStream.WriteAsync(encryptedBytes, cancellationToken).ConfigureAwait(false);
            }

            // Write 0-length end-of-stream marker
            BitConverter.TryWriteBytes(lengthBuffer, 0);
            await outputStream.WriteAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
            await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Decrypts a framed ciphertext stream and writes the plaintext chunks to the output stream.
    /// </summary>
    /// <param name="protector">The data protector instance.</param>
    /// <param name="inputStream">The encrypted stream to read from.</param>
    /// <param name="outputStream">The plaintext stream to write to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task UnprotectStreamAsync(
        this IDataProtector protector,
        Stream inputStream,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(outputStream);

        // 1. Validate stream magic
        var magicBuffer = new byte[StreamMagic.Length];
        await ReadExactAsync(inputStream, magicBuffer, cancellationToken).ConfigureAwait(false);

        if (!magicBuffer.AsSpan().SequenceEqual(StreamMagic))
        {
            throw new CryptographicException("The stream does not begin with valid Kyrolus DataProtection magic bytes.");
        }

        // 2. Validate format version
        var versionBuffer = new byte[1];
        await ReadExactAsync(inputStream, versionBuffer, cancellationToken).ConfigureAwait(false);
        if (versionBuffer[0] != StreamVersion)
        {
            throw new CryptographicException($"Unsupported Kyrolus DataProtection stream format version '{versionBuffer[0]}'.");
        }

        var lengthBuffer = new byte[4];

        while (true)
        {
            await ReadExactAsync(inputStream, lengthBuffer, cancellationToken).ConfigureAwait(false);
            var chunkLength = BitConverter.ToInt32(lengthBuffer, 0);

            if (chunkLength == 0)
            {
                // End of stream reached
                break;
            }

            if (chunkLength < 0 || chunkLength > MaxAllowedChunkSize)
            {
                throw new CryptographicException($"Invalid chunk size '{chunkLength}' encountered in DataProtection stream.");
            }

            var chunkCipher = new byte[chunkLength];
            await ReadExactAsync(inputStream, chunkCipher, cancellationToken).ConfigureAwait(false);

            var plainChunk = protector.Unprotect(chunkCipher);
            await outputStream.WriteAsync(plainChunk, cancellationToken).ConfigureAwait(false);
        }

        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new CryptographicException("Unexpected end of DataProtection stream reached while reading framed chunk.");
            }
            totalRead += read;
        }
    }
}
