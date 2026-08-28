using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace KyrolusSous.DataProtection.Vault;

internal sealed class KyrolusVaultXmlEncryptor : IXmlEncryptor
{
    private const string RootElement = "encryptedKey";
    private const string ProviderElement = "vault";
    private const string KeyNameElement = "keyName";
    private const string MountPathElement = "mountPath";
    private const string CiphertextElement = "ciphertext";

    private readonly HttpClient _httpClient;
    private readonly KyrolusVaultOptions _options;

    public KyrolusVaultXmlEncryptor(HttpClient httpClient, KyrolusVaultOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.VaultAddress))
        {
            throw new ArgumentException("VaultAddress is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(_options.KeyName))
        {
            throw new ArgumentException("KeyName is required.", nameof(options));
        }
    }

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var xmlString = plaintextElement.ToString(SaveOptions.DisableFormatting);
        var base64Plaintext = Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlString));

        var mount = string.IsNullOrWhiteSpace(_options.MountPath) ? "transit" : _options.MountPath.Trim('/');
        var requestUrl = $"{_options.VaultAddress.TrimEnd('/')}/v1/{mount}/encrypt/{_options.KeyName}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { plaintext = base64Plaintext }),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            request.Headers.Add("X-Vault-Token", _options.Token);
        }

        using var response = _httpClient.Send(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var errorMessage = TryExtractVaultError(errorBody) ?? response.ReasonPhrase ?? "Unknown Vault Error";
            throw new CryptographicException($"Vault transit encrypt failed ({(int)response.StatusCode} {response.StatusCode}): {errorMessage}");
        }

        using var stream = response.Content.ReadAsStream();
        using var doc = JsonDocument.Parse(stream);

        var ciphertext = doc.RootElement
            .GetProperty("data")
            .GetProperty("ciphertext")
            .GetString() ?? throw new InvalidOperationException("Vault did not return a valid ciphertext.");

        var encryptedElement = new XElement(
            RootElement,
            new XElement(
                ProviderElement,
                new XElement(KeyNameElement, _options.KeyName),
                new XElement(MountPathElement, _options.MountPath)),
            new XElement(CiphertextElement, ciphertext));

        return new EncryptedXmlInfo(encryptedElement, typeof(KyrolusVaultXmlDecryptor));
    }

    internal static string? TryExtractVaultError(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var err in errorsProp.EnumerateArray())
                {
                    var s = err.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
                return string.Join("; ", list);
            }
        }
        catch { }
        return null;
    }
}

internal sealed class KyrolusVaultXmlDecryptor : IXmlDecryptor
{
    private const string ProviderElement = "vault";
    private const string KeyNameElement = "keyName";
    private const string MountPathElement = "mountPath";
    private const string CiphertextElement = "ciphertext";

    private readonly HttpClient _httpClient;
    private readonly KyrolusVaultOptions _options;

    public KyrolusVaultXmlDecryptor(HttpClient httpClient, KyrolusVaultOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var provider = encryptedElement.Element(ProviderElement)
            ?? throw new InvalidDataException("Missing vault element in encrypted data protection key.");

        var keyName = provider.Element(KeyNameElement)?.Value ?? _options.KeyName;
        var mountPath = provider.Element(MountPathElement)?.Value ?? _options.MountPath;
        var ciphertext = encryptedElement.Element(CiphertextElement)?.Value
            ?? throw new InvalidDataException("Missing ciphertext element in encrypted data protection key.");

        var mount = string.IsNullOrWhiteSpace(mountPath) ? "transit" : mountPath.Trim().Trim('/');
        var key = string.IsNullOrWhiteSpace(keyName) ? _options.KeyName : keyName.Trim().Trim('/');
        var requestUrl = $"{_options.VaultAddress.TrimEnd('/')}/v1/{mount}/decrypt/{key}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { ciphertext }),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            request.Headers.Add("X-Vault-Token", _options.Token);
        }

        using var response = _httpClient.Send(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var errorMessage = KyrolusVaultXmlEncryptor.TryExtractVaultError(errorBody) ?? response.ReasonPhrase ?? "Unknown Vault Error";
            throw new CryptographicException($"Vault transit decrypt failed ({(int)response.StatusCode} {response.StatusCode}): {errorMessage}");
        }

        using var stream = response.Content.ReadAsStream();
        using var doc = JsonDocument.Parse(stream);

        var base64Plaintext = doc.RootElement
            .GetProperty("data")
            .GetProperty("plaintext")
            .GetString() ?? throw new InvalidOperationException("Vault did not return valid decrypted plaintext.");

        var xmlBytes = Convert.FromBase64String(base64Plaintext);
        var xmlString = Encoding.UTF8.GetString(xmlBytes);

        return XElement.Parse(xmlString);
    }
}
