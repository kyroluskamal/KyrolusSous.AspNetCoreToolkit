using System.Security.Cryptography;

namespace KyrolusSous.Caching.Abstractions;

public sealed class KyrolusAesCachePayloadTransformer : IKyrolusCachePayloadTransformer
{
    private const int AesBlockSizeBytes = 16;
    private readonly byte[] key;
    private readonly byte[]? staticIv;

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

    public byte[] Transform(byte[] payload)
    {
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

    public byte[] Restore(byte[] payload)
    {
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
