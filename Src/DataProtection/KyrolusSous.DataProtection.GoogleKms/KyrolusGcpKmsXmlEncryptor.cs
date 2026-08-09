using System;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace KyrolusSous.DataProtection.GoogleKms;

internal sealed class KyrolusGcpKmsXmlEncryptor : IXmlEncryptor
{
    private const int TagSize = 16;
    private const string RootElement = "encryptedKey";
    private const string ProviderElement = "gcpKms";
    private const string KeyNameElement = "keyName";
    private const string DataKeyElement = "dataKey";
    private const string NonceElement = "nonce";
    private const string TagElement = "tag";
    private const string CiphertextElement = "ciphertext";

    private readonly KeyManagementServiceClient kmsClient;
    private readonly KyrolusGcpKmsOptions options;

    public KyrolusGcpKmsXmlEncryptor(KeyManagementServiceClient kmsClient, KyrolusGcpKmsOptions options)
    {
        this.kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(this.options.CryptoKeyName))
        {
            throw new ArgumentException("CryptoKey name is required.", nameof(options));
        }
    }

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (plaintextElement is null) throw new ArgumentNullException(nameof(plaintextElement));

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var dataKeyPlain = new byte[32];
        RandomNumberGenerator.Fill(dataKeyPlain);

        var encryptResponse = kmsClient.Encrypt(new EncryptRequest
        {
            Name = options.CryptoKeyName,
            Plaintext = ByteString.CopyFrom(dataKeyPlain)
        });

        var encryptedKey = encryptResponse.Ciphertext.ToByteArray();

        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintextBytes.Length];

        try
        {
            using var aes = new AesGcm(dataKeyPlain, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKeyPlain);
        }

        var encryptedElement = new XElement(
            RootElement,
            new XElement(
                ProviderElement,
                new XElement(KeyNameElement, options.CryptoKeyName),
                new XElement(DataKeyElement, Convert.ToBase64String(encryptedKey)),
                new XElement(NonceElement, Convert.ToBase64String(nonce)),
                new XElement(TagElement, Convert.ToBase64String(tag))),
            new XElement(CiphertextElement, Convert.ToBase64String(ciphertext)));

        return new EncryptedXmlInfo(encryptedElement, typeof(KyrolusGcpKmsXmlDecryptor));
    }

    internal static byte[] GetRequiredBase64(XElement parent, string name)
    {
        var value = parent.Element(name)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Missing '{name}' element.");
        }

        return Convert.FromBase64String(value);
    }
}

internal sealed class KyrolusGcpKmsXmlDecryptor : IXmlDecryptor
{
    private const int TagSize = 16;
    private const string ProviderElement = "gcpKms";
    private const string KeyNameElement = "keyName";
    private const string DataKeyElement = "dataKey";
    private const string NonceElement = "nonce";
    private const string TagElement = "tag";
    private const string CiphertextElement = "ciphertext";

    private readonly KeyManagementServiceClient kmsClient;
    private readonly KyrolusGcpKmsOptions options;

    public KyrolusGcpKmsXmlDecryptor(KeyManagementServiceClient kmsClient, KyrolusGcpKmsOptions options)
    {
        this.kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        if (encryptedElement is null) throw new ArgumentNullException(nameof(encryptedElement));

        var provider = encryptedElement.Element(ProviderElement)
            ?? throw new InvalidDataException("Missing gcpKms element.");

        var keyName = provider.Element(KeyNameElement)?.Value;
        if (string.IsNullOrWhiteSpace(keyName))
        {
            keyName = options.CryptoKeyName;
        }

        var encryptedKey = KyrolusGcpKmsXmlEncryptor.GetRequiredBase64(provider, DataKeyElement);
        var nonce = KyrolusGcpKmsXmlEncryptor.GetRequiredBase64(provider, NonceElement);
        var tag = KyrolusGcpKmsXmlEncryptor.GetRequiredBase64(provider, TagElement);
        var ciphertext = KyrolusGcpKmsXmlEncryptor.GetRequiredBase64(encryptedElement, CiphertextElement);

        var decryptResponse = kmsClient.Decrypt(new DecryptRequest
        {
            Name = keyName,
            Ciphertext = ByteString.CopyFrom(encryptedKey)
        });

        var dataKeyPlain = decryptResponse.Plaintext.ToByteArray();
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(dataKeyPlain, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKeyPlain);
        }

        var xml = Encoding.UTF8.GetString(plaintext);
        return XElement.Parse(xml);
    }
}
