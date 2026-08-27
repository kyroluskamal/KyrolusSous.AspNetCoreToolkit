using System.Security.Cryptography;
using System.Text;
using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Tokens;

/// <summary>
/// Service contract for generating and validating cryptographically signed user tokens bound to specific purposes and security stamps.
/// </summary>
public interface IKyrolusUserTokenService
{
    /// <summary>
    /// Generates a signed token for a specific user and purpose.
    /// </summary>
    string GenerateToken(KyrolusAuthUser user, string purpose, TimeSpan? customLifetime = null);

    /// <summary>
    /// Validates a user-supplied token against the user, purpose, expiration, and current security stamp.
    /// </summary>
    bool ValidateToken(KyrolusAuthUser user, string purpose, string token);
}

/// <summary>
/// High-performance, AOT-friendly implementation of <see cref="IKyrolusUserTokenService"/> using HMAC-SHA256.
/// </summary>
public sealed class KyrolusUserTokenService : IKyrolusUserTokenService
{
    private readonly KyrolusUserTokenOptions _options;
    private readonly byte[] _keyBytes;

    public KyrolusUserTokenService(KyrolusUserTokenOptions? options = null)
    {
        _options = options ?? new KyrolusUserTokenOptions();

        if (string.IsNullOrWhiteSpace(_options.SecretKey) || _options.SecretKey.Length < 32)
        {
            throw new ArgumentException("SecretKey must be at least 32 characters (256 bits) long.", nameof(options));
        }

        _keyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
    }

    public string GenerateToken(KyrolusAuthUser user, string purpose, TimeSpan? customLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var lifetime = customLifetime ?? purpose switch
        {
            KyrolusTokenPurposes.PasswordReset => _options.PasswordResetLifetime,
            KyrolusTokenPurposes.EmailConfirmation => _options.EmailConfirmationLifetime,
            _ => _options.DefaultLifetime
        };

        var expiresUnixSeconds = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var stamp = user.SecurityStamp ?? string.Empty;

        // Payload format: EscapedUserId|ExpiresUnixSeconds|EscapedPurpose|EscapedSecurityStamp
        var payloadString = $"{Uri.EscapeDataString(user.Id)}|{expiresUnixSeconds}|{Uri.EscapeDataString(purpose)}|{Uri.EscapeDataString(stamp)}";
        var payloadBytes = Encoding.UTF8.GetBytes(payloadString);
        var payloadBase64 = ToBase64Url(payloadBytes);

        var signature = ComputeHmac(payloadBytes);
        var signatureBase64 = ToBase64Url(signature);

        return $"{payloadBase64}.{signatureBase64}";
    }

    public bool ValidateToken(KyrolusAuthUser user, string purpose, string token)
    {
        if (user is null || string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] providedSignature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            providedSignature = FromBase64Url(parts[1]);
        }
        catch
        {
            return false;
        }

        // Verify cryptographic signature first
        var expectedSignature = ComputeHmac(payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return false;
        }

        // Parse payload safely
        string payloadString;
        try
        {
            payloadString = Encoding.UTF8.GetString(payloadBytes);
        }
        catch
        {
            return false;
        }

        var segments = payloadString.Split('|');
        if (segments.Length != 4)
        {
            return false;
        }

        string userId;
        string expiresUnixStr;
        string tokenPurpose;
        string tokenSecurityStamp;
        try
        {
            userId = Uri.UnescapeDataString(segments[0]);
            expiresUnixStr = segments[1];
            tokenPurpose = Uri.UnescapeDataString(segments[2]);
            tokenSecurityStamp = Uri.UnescapeDataString(segments[3]);
        }
        catch
        {
            return false;
        }

        // 1. Verify User ID
        if (!string.Equals(userId, user.Id, StringComparison.Ordinal))
        {
            return false;
        }

        // 2. Verify Purpose
        if (!string.Equals(tokenPurpose, purpose, StringComparison.Ordinal))
        {
            return false;
        }

        // 3. Verify Expiry (with clock skew tolerance)
        var skewSeconds = (long)Math.Max(0, _options.ClockSkew.TotalSeconds);
        if (!long.TryParse(expiresUnixStr, out var expiresUnix) || expiresUnix <= 0 || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - skewSeconds > expiresUnix)
        {
            return false;
        }

        // 4. Verify Security Stamp (invalidates token if user changed password/stamp)
        var currentStamp = user.SecurityStamp ?? string.Empty;
        if (!string.Equals(tokenSecurityStamp, currentStamp, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private byte[] ComputeHmac(byte[] data)
    {
        using var hmac = new HMACSHA256(_keyBytes);
        return hmac.ComputeHash(data);
    }

    private static string ToBase64Url(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static byte[] FromBase64Url(string base64Url)
    {
        var padded = base64Url.Replace("-", "+").Replace("_", "/");
        switch (padded.Length % 4)
        {
            case 1: return [];
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
