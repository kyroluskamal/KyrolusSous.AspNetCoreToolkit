using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusEncryptedFileKeyEscrowSink(
    string directoryPath,
    KyrolusDataProtectionKeyEscrowEncryptionOptions options)
    : IKeyEscrowSink
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly string directoryPath = string.IsNullOrWhiteSpace(directoryPath)
        ? throw new ArgumentException("Directory path is required.", nameof(directoryPath))
        : directoryPath;
    private readonly KyrolusDataProtectionKeyEscrowEncryptionOptions options =
        options ?? throw new ArgumentNullException(nameof(options));

    public void Store(Guid keyId, XElement element)
    {
        Directory.CreateDirectory(directoryPath);

        var filePath = Path.Combine(directoryPath, $"{keyId}.xml");
        var xml = element.ToString(SaveOptions.DisableFormatting);
        var payload = options.Enabled
            ? EncryptPayload(xml)
            : xml;

        File.WriteAllText(filePath, payload);
    }

    private string EncryptPayload(string xml)
    {
        var key = ResolveKey();
        var plaintext = Encoding.UTF8.GetBytes(xml);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plaintext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag);

        var combined = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, combined, nonce.Length + tag.Length, cipher.Length);

        return options.PayloadPrefix + Convert.ToBase64String(combined);
    }

    private byte[] ResolveKey()
    {
        if (options.EncryptionKey is { Length: > 0 })
        {
            ValidateKey(options.EncryptionKey);
            return options.EncryptionKey;
        }

        if (!string.IsNullOrWhiteSpace(options.EncryptionKeyBase64))
        {
            var key = Convert.FromBase64String(options.EncryptionKeyBase64);
            ValidateKey(key);
            return key;
        }

        throw new InvalidOperationException(
            "Escrow encryption is enabled but no encryption key was provided.");
    }

    private static void ValidateKey(byte[] key)
    {
        if (key.Length is 16 or 24 or 32)
        {
            return;
        }

        throw new InvalidOperationException(
            "Escrow encryption key must be 16, 24, or 32 bytes.");
    }
}
