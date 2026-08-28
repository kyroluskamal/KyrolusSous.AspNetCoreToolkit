using System.Security.Cryptography;
using KyrolusSous.RabbitMQ.Abstractions.Security;

namespace KyrolusSous.RabbitMQ.Runtime.Security;

/// <summary>
/// High-security AES-GCM message payload encryptor with key zeroization upon disposal.
/// </summary>
public class KyrolusAesMessageEncryptor : IKyrolusMessageEncryptor, IDisposable
{
    private readonly byte[] _key;
    private const int NonceSize = 12; // 96-bit nonce for AES-GCM
    private const int TagSize = 16;   // 128-bit authentication tag
    private bool _disposed;

    public KyrolusAesMessageEncryptor(byte[] key)
    {
        if (key is null || (key.Length != 16 && key.Length != 24 && key.Length != 32))
        {
            throw new ArgumentException("Key must be 128, 192, or 256 bits (16, 24, or 32 bytes).", nameof(key));
        }

        // Check weak key (all zeros)
        bool allZeroes = true;
        for (int i = 0; i < key.Length; i++)
        {
            if (key[i] != 0)
            {
                allZeroes = false;
                break;
            }
        }

        if (allZeroes)
        {
            throw new ArgumentException("Weak key detected: all bytes are zero.", nameof(key));
        }

        _key = (byte[])key.Clone();
    }

    public KyrolusAesMessageEncryptor(string keyBase64)
        : this(Convert.FromBase64String(keyBase64))
    {
    }

    public byte[] Encrypt(byte[] plainBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plainBytes);

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[TagSize];
        var cipherBytes = new byte[plainBytes.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Combined output: Nonce + Tag + Ciphertext
        var result = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize + TagSize, cipherBytes.Length);

        return result;
    }

    public byte[] Decrypt(byte[] cipherBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(cipherBytes);

        if (cipherBytes.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Ciphertext is too short to contain valid nonce and authentication tag.");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherTextLength = cipherBytes.Length - NonceSize - TagSize;
        var encryptedData = new byte[cipherTextLength];
        var plainBytes = new byte[cipherTextLength];

        Buffer.BlockCopy(cipherBytes, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(cipherBytes, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(cipherBytes, NonceSize + TagSize, encryptedData, 0, cipherTextLength);

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, encryptedData, tag, plainBytes);

        return plainBytes;
    }

    public ValueTask<byte[]> EncryptAsync(byte[] plainBytes, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Encrypt(plainBytes));
    }

    public ValueTask<byte[]> DecryptAsync(byte[] cipherBytes, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Decrypt(cipherBytes));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CryptographicOperations.ZeroMemory(_key);
        GC.SuppressFinalize(this);
    }
}
