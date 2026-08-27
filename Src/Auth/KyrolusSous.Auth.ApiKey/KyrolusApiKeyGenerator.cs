using System.Security.Cryptography;
using System.Text;

namespace KyrolusSous.Auth.ApiKey;

public sealed class KyrolusApiKeyGenerator : IKyrolusApiKeyGenerator
{
    public string GenerateKey(string prefix = "kyr_")
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (prefix.Length > 32 || prefix.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw new ArgumentException("API key prefix cannot contain whitespace or control characters and must not exceed 32 characters.", nameof(prefix));
        }

        var randomBytes = new byte[32]; // 256 bits of entropy
        RandomNumberGenerator.Fill(randomBytes);
        var base64Url = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        return $"{prefix}{base64Url}";
    }

    public string HashKey(string rawKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawKey);
        if (rawKey.Length > 512)
        {
            throw new ArgumentException("API key exceeds maximum permitted length of 512 characters.", nameof(rawKey));
        }

        var bytes = Encoding.UTF8.GetBytes(rawKey.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
