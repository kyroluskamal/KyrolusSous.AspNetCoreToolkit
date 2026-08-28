using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.Runtime;

/// <summary>
/// Enterprise extension methods for <see cref="IDataProtector"/>.
/// </summary>
public static class KyrolusDataProtectionExtensions
{
    private const char ExpirySeparator = '\0';

    #region String & Byte Unprotection

    /// <summary>
    /// Safely attempts to unprotect string data without throwing exceptions on corrupt or invalid payloads.
    /// </summary>
    public static bool TryUnprotect(
        this IDataProtector protector,
        string? protectedData,
        [NotNullWhen(true)] out string? unprotectedData)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (string.IsNullOrWhiteSpace(protectedData))
        {
            unprotectedData = null;
            return false;
        }

        try
        {
            unprotectedData = protector.Unprotect(protectedData);
            return true;
        }
        catch
        {
            unprotectedData = null;
            return false;
        }
    }

    /// <summary>
    /// Safely attempts to unprotect byte array data without throwing exceptions on corrupt or invalid payloads.
    /// </summary>
    public static bool TryUnprotect(
        this IDataProtector protector,
        byte[]? protectedData,
        [NotNullWhen(true)] out byte[]? unprotectedData)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (protectedData is null || protectedData.Length == 0)
        {
            unprotectedData = null;
            return false;
        }

        try
        {
            unprotectedData = protector.Unprotect(protectedData);
            return true;
        }
        catch
        {
            unprotectedData = null;
            return false;
        }
    }

    #endregion

    #region Time-Limited Expiration

    /// <summary>
    /// Protects plaintext with a specified expiration lifetime.
    /// </summary>
    public static string ProtectWithExpiry(
        this IDataProtector protector,
        string plaintext,
        TimeSpan lifetime)
    {
        return protector.ProtectWithExpiry(plaintext, DateTimeOffset.UtcNow.Add(lifetime));
    }

    /// <summary>
    /// Protects plaintext with a specified expiration timestamp.
    /// </summary>
    public static string ProtectWithExpiry(
        this IDataProtector protector,
        string plaintext,
        DateTimeOffset expiration)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(plaintext);

        var expiryUnixMs = expiration.ToUnixTimeMilliseconds();
        var combinedPayload = $"{expiryUnixMs}{ExpirySeparator}{plaintext}";
        return protector.Protect(combinedPayload);
    }

    /// <summary>
    /// Unprotects a time-limited payload, throwing <see cref="CryptographicException"/> if expired or corrupt.
    /// </summary>
    public static string UnprotectWithExpiry(
        this IDataProtector protector,
        string protectedData,
        TimeSpan clockSkew = default)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedData);

        var combinedPayload = protector.Unprotect(protectedData);
        var separatorIndex = combinedPayload.IndexOf(ExpirySeparator);

        if (separatorIndex <= 0)
        {
            throw new CryptographicException("The protected payload is not in a valid time-limited format.");
        }

        if (!long.TryParse(combinedPayload[..separatorIndex], out var expiryUnixMs))
        {
            throw new CryptographicException("The protected payload contains an invalid expiration timestamp.");
        }

        var currentUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var allowedSkewMs = Math.Max(0, (long)clockSkew.TotalMilliseconds);
        if (currentUnixMs - allowedSkewMs > expiryUnixMs)
        {
            throw new CryptographicException("The payload has expired.");
        }

        return combinedPayload[(separatorIndex + 1)..];
    }

    /// <summary>
    /// Safely unprotects a time-limited payload, returning false if expired, corrupt, or invalid.
    /// </summary>
    public static bool TryUnprotectWithExpiry(
        this IDataProtector protector,
        string? protectedData,
        [NotNullWhen(true)] out string? unprotectedData)
    {
        return protector.TryUnprotectWithExpiry(protectedData, TimeSpan.Zero, out unprotectedData);
    }

    /// <summary>
    /// Safely unprotects a time-limited payload with optional clock skew, returning false if expired, corrupt, or invalid.
    /// </summary>
    public static bool TryUnprotectWithExpiry(
        this IDataProtector protector,
        string? protectedData,
        TimeSpan clockSkew,
        [NotNullWhen(true)] out string? unprotectedData)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (string.IsNullOrWhiteSpace(protectedData))
        {
            unprotectedData = null;
            return false;
        }

        try
        {
            unprotectedData = protector.UnprotectWithExpiry(protectedData, clockSkew);
            return true;
        }
        catch
        {
            unprotectedData = null;
            return false;
        }
    }

    #endregion

    #region Base64Url Web-Safe Protection

    /// <summary>
    /// Protects plaintext and returns a URL-safe Base64Url string suitable for query parameters and route segments.
    /// </summary>
    public static string ProtectAsBase64Url(this IDataProtector protector, string plaintext)
    {
        ArgumentNullException.ThrowIfNull(protector);
        var ciphertext = protector.Protect(plaintext);
        return Base64ToBase64Url(ciphertext);
    }

    /// <summary>
    /// Unprotects a URL-safe Base64Url string.
    /// </summary>
    public static string UnprotectFromBase64Url(this IDataProtector protector, string base64UrlData)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentException.ThrowIfNullOrWhiteSpace(base64UrlData);

        var base64 = Base64UrlToBase64(base64UrlData);
        return protector.Unprotect(base64);
    }

    /// <summary>
    /// Safely attempts to unprotect a URL-safe Base64Url string without throwing exceptions.
    /// </summary>
    public static bool TryUnprotectFromBase64Url(
        this IDataProtector protector,
        string? base64UrlData,
        [NotNullWhen(true)] out string? unprotectedData)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (string.IsNullOrWhiteSpace(base64UrlData))
        {
            unprotectedData = null;
            return false;
        }

        try
        {
            unprotectedData = protector.UnprotectFromBase64Url(base64UrlData);
            return true;
        }
        catch
        {
            unprotectedData = null;
            return false;
        }
    }

    /// <summary>
    /// Protects plaintext with expiration and encodes it as a URL-safe Base64Url string.
    /// </summary>
    public static string ProtectWithExpiryAsBase64Url(
        this IDataProtector protector,
        string plaintext,
        TimeSpan lifetime)
    {
        var cipher = protector.ProtectWithExpiry(plaintext, lifetime);
        return Base64ToBase64Url(cipher);
    }

    /// <summary>
    /// Protects plaintext with expiration timestamp and encodes it as a URL-safe Base64Url string.
    /// </summary>
    public static string ProtectWithExpiryAsBase64Url(
        this IDataProtector protector,
        string plaintext,
        DateTimeOffset expiration)
    {
        var cipher = protector.ProtectWithExpiry(plaintext, expiration);
        return Base64ToBase64Url(cipher);
    }

    /// <summary>
    /// Unprotects a time-limited URL-safe Base64Url payload.
    /// </summary>
    public static string UnprotectWithExpiryFromBase64Url(
        this IDataProtector protector,
        string base64UrlData)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentException.ThrowIfNullOrWhiteSpace(base64UrlData);

        var base64 = Base64UrlToBase64(base64UrlData);
        return protector.UnprotectWithExpiry(base64);
    }

    /// <summary>
    /// Safely attempts to unprotect a time-limited URL-safe Base64Url payload.
    /// </summary>
    public static bool TryUnprotectWithExpiryFromBase64Url(
        this IDataProtector protector,
        string? base64UrlData,
        [NotNullWhen(true)] out string? unprotectedData)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (string.IsNullOrWhiteSpace(base64UrlData))
        {
            unprotectedData = null;
            return false;
        }

        try
        {
            unprotectedData = protector.UnprotectWithExpiryFromBase64Url(base64UrlData);
            return true;
        }
        catch
        {
            unprotectedData = null;
            return false;
        }
    }

    #endregion

    #region Generic Object/Record Protection

    /// <summary>
    /// Serializes an object to JSON and encrypts it into a protected string.
    /// </summary>
    public static string ProtectObject<T>(
        this IDataProtector protector,
        T value,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(value);

        var json = JsonSerializer.Serialize(value, serializerOptions);
        return protector.Protect(json);
    }

    /// <summary>
    /// Decrypts a protected payload and deserializes it back to an object.
    /// </summary>
    public static T UnprotectObject<T>(
        this IDataProtector protector,
        string protectedData,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedData);

        var json = protector.Unprotect(protectedData);
        var result = JsonSerializer.Deserialize<T>(json, serializerOptions);
        if (result is null)
        {
            throw new CryptographicException($"Failed to deserialize protected JSON payload to type '{typeof(T).Name}'.");
        }

        return result;
    }

    /// <summary>
    /// Safely attempts to decrypt and deserialize an object without throwing exceptions on error.
    /// </summary>
    public static bool TryUnprotectObject<T>(
        this IDataProtector protector,
        string? protectedData,
        [NotNullWhen(true)] out T? value,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (string.IsNullOrWhiteSpace(protectedData))
        {
            value = default;
            return false;
        }

        try
        {
            value = protector.UnprotectObject<T>(protectedData, serializerOptions);
            return value is not null;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Serializes an object to JSON and encrypts it with an expiration lifetime.
    /// </summary>
    public static string ProtectObjectWithExpiry<T>(
        this IDataProtector protector,
        T value,
        TimeSpan lifetime,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(value);

        var json = JsonSerializer.Serialize(value, serializerOptions);
        return protector.ProtectWithExpiry(json, lifetime);
    }

    /// <summary>
    /// Serializes an object to JSON and encrypts it with an expiration timestamp.
    /// </summary>
    public static string ProtectObjectWithExpiry<T>(
        this IDataProtector protector,
        T value,
        DateTimeOffset expiration,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(value);

        var json = JsonSerializer.Serialize(value, serializerOptions);
        return protector.ProtectWithExpiry(json, expiration);
    }

    /// <summary>
    /// Decrypts a time-limited payload and deserializes it back to an object.
    /// </summary>
    public static T UnprotectObjectWithExpiry<T>(
        this IDataProtector protector,
        string protectedData,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedData);

        var json = protector.UnprotectWithExpiry(protectedData);
        var result = JsonSerializer.Deserialize<T>(json, serializerOptions);
        if (result is null)
        {
            throw new CryptographicException($"Failed to deserialize protected JSON payload to type '{typeof(T).Name}'.");
        }

        return result;
    }

    /// <summary>
    /// Safely attempts to decrypt and deserialize a time-limited payload.
    /// </summary>
    public static bool TryUnprotectObjectWithExpiry<T>(
        this IDataProtector protector,
        string? protectedData,
        [NotNullWhen(true)] out T? value,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (string.IsNullOrWhiteSpace(protectedData))
        {
            value = default;
            return false;
        }

        try
        {
            value = protector.UnprotectObjectWithExpiry<T>(protectedData, serializerOptions);
            return value is not null;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    #endregion

    #region Re-encryption & Migration

    /// <summary>
    /// Re-encrypts an existing protected ciphertext with the currently active key in the keyring.
    /// </summary>
    public static string ReEncrypt(
        this IDataProtector protector,
        string protectedData)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedData);

        var plaintext = protector.Unprotect(protectedData);
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Safely attempts to re-encrypt an existing ciphertext under the active key.
    /// </summary>
    public static bool TryReEncrypt(
        this IDataProtector protector,
        string? protectedData,
        [NotNullWhen(true)] out string? reEncryptedData)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (protector.TryUnprotect(protectedData, out var plaintext))
        {
            reEncryptedData = protector.Protect(plaintext);
            return true;
        }

        reEncryptedData = null;
        return false;
    }

    #endregion

    #region Factory & Tenant Helpers

    /// <summary>
    /// Creates a type-safe data protector based on the type's full name and optional sub-purpose.
    /// </summary>
    public static IDataProtector CreateProtector<T>(
        this IKyrolusDataProtectorFactory factory,
        string? subPurpose = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var basePurpose = typeof(T).FullName ?? typeof(T).Name;
        if (string.IsNullOrWhiteSpace(basePurpose))
        {
            basePurpose = "AnonymousType";
        }

        var fullPurpose = string.IsNullOrWhiteSpace(subPurpose)
            ? basePurpose
            : $"{basePurpose}.{subPurpose.Trim()}";

        return factory.CreateProtector(fullPurpose);
    }

    /// <summary>
    /// Creates a tenant-isolated data protector with a fluent helper.
    /// </summary>
    public static IDataProtector CreateProtectorForTenant(
        this IKyrolusTenantDataProtectionProvider provider,
        string tenantId,
        string purpose)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        return provider.CreateProtector(tenantId.Trim(), purpose.Trim());
    }

    #endregion

    #region Private Base64Url Helpers

    private static string Base64ToBase64Url(string base64)
    {
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Base64UrlToBase64(string base64Url)
    {
        var base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        return (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            1 => throw new FormatException("Invalid Base64Url string length (modulo 4 cannot be 1)."),
            _ => base64
        };
    }

    #endregion
}
