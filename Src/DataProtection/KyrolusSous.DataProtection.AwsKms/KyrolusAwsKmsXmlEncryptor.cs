using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace KyrolusSous.DataProtection.AwsKms;

internal sealed class KyrolusAwsKmsXmlEncryptor : IXmlEncryptor
{
    private const int TagSize = 16;
    private const string RootElement = "encryptedKey";
    private const string ProviderElement = "awsKms";
    private const string KeyIdElement = "keyId";
    private const string DataKeyElement = "dataKey";
    private const string NonceElement = "nonce";
    private const string TagElement = "tag";
    private const string CiphertextElement = "ciphertext";

    private readonly IAmazonKeyManagementService kmsClient;
    private readonly KyrolusAwsKmsOptions options;

    public KyrolusAwsKmsXmlEncryptor(
        IAmazonKeyManagementService kmsClient,
        KyrolusAwsKmsOptions options)
    {
        this.kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(this.options.KeyId))
        {
            throw new ArgumentException("KeyId is required.", nameof(options));
        }
    }

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (plaintextElement is null) throw new ArgumentNullException(nameof(plaintextElement));

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var dataKeyResponse = kmsClient.GenerateDataKeyAsync(new GenerateDataKeyRequest
        {
            KeyId = options.KeyId,
            KeySpec = DataKeySpec.AES_256,
            EncryptionContext = ToDictionary(options.EncryptionContext)
        }).GetAwaiter().GetResult();

        var dataKeyPlain = dataKeyResponse.Plaintext.ToArray();
        var encryptedKey = dataKeyResponse.CiphertextBlob.ToArray();

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
                new XElement(KeyIdElement, options.KeyId),
                new XElement(DataKeyElement, Convert.ToBase64String(encryptedKey)),
                new XElement(NonceElement, Convert.ToBase64String(nonce)),
                new XElement(TagElement, Convert.ToBase64String(tag))),
            new XElement(CiphertextElement, Convert.ToBase64String(ciphertext)));

        return new EncryptedXmlInfo(encryptedElement, typeof(KyrolusAwsKmsXmlDecryptor));
    }

    private static Dictionary<string, string>? ToDictionary(IReadOnlyDictionary<string, string>? context)
    {
        return context is null ? null : new Dictionary<string, string>(context);
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

internal sealed class KyrolusAwsKmsXmlDecryptor : IXmlDecryptor
{
    private const int TagSize = 16;
    private const string ProviderElement = "awsKms";
    private const string DataKeyElement = "dataKey";
    private const string NonceElement = "nonce";
    private const string TagElement = "tag";
    private const string CiphertextElement = "ciphertext";

    private readonly IAmazonKeyManagementService kmsClient;
    private readonly KyrolusAwsKmsOptions options;

    public KyrolusAwsKmsXmlDecryptor(
        IAmazonKeyManagementService kmsClient,
        KyrolusAwsKmsOptions options)
    {
        this.kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        if (encryptedElement is null) throw new ArgumentNullException(nameof(encryptedElement));

        var provider = encryptedElement.Element(ProviderElement)
            ?? throw new InvalidDataException("Missing awsKms element.");

        var encryptedKey = KyrolusAwsKmsXmlEncryptor.GetRequiredBase64(provider, DataKeyElement);
        var nonce = KyrolusAwsKmsXmlEncryptor.GetRequiredBase64(provider, NonceElement);
        var tag = KyrolusAwsKmsXmlEncryptor.GetRequiredBase64(provider, TagElement);
        var ciphertext = KyrolusAwsKmsXmlEncryptor.GetRequiredBase64(encryptedElement, CiphertextElement);

        var decryptResponse = kmsClient.DecryptAsync(new DecryptRequest
        {
            CiphertextBlob = new MemoryStream(encryptedKey),
            KeyId = options.KeyId,
            EncryptionContext = ToDictionary(options.EncryptionContext)
        }).GetAwaiter().GetResult();

        var dataKeyPlain = decryptResponse.Plaintext.ToArray();
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

    private static Dictionary<string, string>? ToDictionary(IReadOnlyDictionary<string, string>? context)
    {
        return context is null ? null : new Dictionary<string, string>(context);
    }
}
