namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// A secure payload transformer that encrypts and decrypts cache byte payloads using AES (Advanced Encryption Standard) in CBC mode with PKCS7 padding.
/// </summary>
/// <remarks>
/// <para>
/// <b>Security Architecture:</b>
/// When storing sensitive user information (PII, tokens, financial details) in a shared Redis cache, 
/// encrypting payloads prevents unauthorized data exposure even if the Redis database is accessed directly.
/// </para>
/// <para>
/// <b>IV Handling:</b>
/// If no static IV is provided, a cryptographically secure random 16-byte IV is generated per payload using <see cref="RandomNumberGenerator"/> 
/// and prepended to the ciphertext (<c>[16 bytes IV][Ciphertext bytes]</c>). During <see cref="Restore"/>, the IV is sliced from the front 
/// and used to decrypt the remainder.
/// </para>
/// </remarks>
public sealed class KyrolusAesCachePayloadTransformer : IKyrolusCachePayloadTransformer
{
    private const int AesBlockSizeBytes = 16;
    private readonly byte[] key;
    private readonly byte[]? staticIv;

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusAesCachePayloadTransformer"/> with a cryptographic key and optional static IV.
    /// </summary>
    /// <param name="key">The AES symmetric secret key. Must be exactly 16 bytes (AES-128), 24 bytes (AES-192), or 32 bytes (AES-256).</param>
    /// <param name="iv">Optional static 16-byte initialization vector. If <c>null</c> (recommended), a fresh random IV is generated for each payload.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> or <paramref name="iv"/> has an invalid length.</exception>
    public KyrolusAesCachePayloadTransformer(byte[] key, byte[]? iv = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!IsValidKeySize(key.Length))
        {
            throw new ArgumentException("AES key must be 16, 24, or 32 bytes.", nameof(key));
        }

        if (iv is { Length: > 0 } && iv.Length != AesBlockSizeBytes)
        {
            throw new ArgumentException("AES IV must be 16 bytes.", nameof(iv));
        }

        this.key = key;
        staticIv = iv;
    }

    /// <summary>
    /// Encrypts the plaintext byte payload using AES-CBC.
    /// </summary>
    /// <param name="payload">The unencrypted serialized byte array.</param>
    /// <returns>The encrypted ciphertext bytes (with random IV prepended if no static IV was configured).</returns>
    public byte[] Transform(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var iv = staticIv ?? RandomNumberGenerator.GetBytes(AesBlockSizeBytes);
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(payload, 0, payload.Length);

        if (staticIv is not null)
        {
            return cipher;
        }

        var result = new byte[iv.Length + cipher.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(cipher, 0, result, iv.Length, cipher.Length);
        return result;
    }

    /// <summary>
    /// Decrypts the ciphertext byte payload back to original plaintext bytes.
    /// </summary>
    /// <param name="payload">The encrypted byte array read from cache.</param>
    /// <returns>The decrypted plaintext serialized bytes ready for deserialization.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the encrypted payload is smaller than the required 16-byte IV header.</exception>
    public byte[] Restore(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        byte[] iv;
        byte[] cipher;

        if (staticIv is null)
        {
            if (payload.Length < AesBlockSizeBytes)
            {
                throw new InvalidOperationException("Encrypted payload is too small.");
            }

            iv = new byte[AesBlockSizeBytes];
            Buffer.BlockCopy(payload, 0, iv, 0, AesBlockSizeBytes);
            cipher = new byte[payload.Length - AesBlockSizeBytes];
            Buffer.BlockCopy(payload, AesBlockSizeBytes, cipher, 0, cipher.Length);
        }
        else
        {
            iv = staticIv;
            cipher = payload;
        }

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    private static bool IsValidKeySize(int size) => size is 16 or 24 or 32;
}
