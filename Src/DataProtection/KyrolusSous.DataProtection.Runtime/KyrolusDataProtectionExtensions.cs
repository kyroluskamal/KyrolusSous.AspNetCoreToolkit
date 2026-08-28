using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.Runtime;

/// <summary>
/// Enterprise extension methods for <see cref="IDataProtector"/>.
/// </summary>
public static class KyrolusDataProtectionExtensions
{
    private const char ExpirySeparator = '\0';

    /// <summary>
    /// Safely attempts to unprotect string data without throwing exceptions on corrupt or invalid payloads.
    /// </summary>
    /// <param name="protector">The data protector instance.</param>
    /// <param name="protectedData">The protected ciphertext.</param>
    /// <param name="unprotectedData">The decrypted plaintext if successful.</param>
    /// <returns>True if decryption succeeded; otherwise, false.</returns>
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
        catch (CryptographicException)
        {
            unprotectedData = null;
            return false;
        }
        catch (FormatException)
        {
            unprotectedData = null;
            return false;
        }
        catch (Exception)
        {
            unprotectedData = null;
            return false;
        }
    }

    /// <summary>
    /// Safely attempts to unprotect byte array data without throwing exceptions on corrupt or invalid payloads.
    /// </summary>
    /// <param name="protector">The data protector instance.</param>
    /// <param name="protectedData">The protected ciphertext bytes.</param>
    /// <param name="unprotectedData">The decrypted plaintext bytes if successful.</param>
    /// <returns>True if decryption succeeded; otherwise, false.</returns>
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
        catch (CryptographicException)
        {
            unprotectedData = null;
            return false;
        }
        catch (Exception)
        {
            unprotectedData = null;
            return false;
        }
    }

    /// <summary>
    /// Protects plaintext with a specified expiration lifetime.
    /// </summary>
    /// <param name="protector">The data protector instance.</param>
    /// <param name="plaintext">The plaintext to protect.</param>
    /// <param name="lifetime">The lifetime duration until expiration.</param>
    /// <returns>The protected self-expiring payload.</returns>
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
    /// <param name="protector">The data protector instance.</param>
    /// <param name="plaintext">The plaintext to protect.</param>
    /// <param name="expiration">The exact expiration timestamp.</param>
    /// <returns>The protected self-expiring payload.</returns>
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
    /// <param name="protector">The data protector instance.</param>
    /// <param name="protectedData">The protected time-limited ciphertext.</param>
    /// <returns>The decrypted plaintext.</returns>
    public static string UnprotectWithExpiry(
        this IDataProtector protector,
        string protectedData)
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
        if (currentUnixMs > expiryUnixMs)
        {
            throw new CryptographicException("The payload has expired.");
        }

        return combinedPayload[(separatorIndex + 1)..];
    }

    /// <summary>
    /// Safely unprotects a time-limited payload, returning false if expired, corrupt, or invalid.
    /// </summary>
    /// <param name="protector">The data protector instance.</param>
    /// <param name="protectedData">The protected time-limited ciphertext.</param>
    /// <param name="unprotectedData">The decrypted plaintext if valid and unexpired.</param>
    /// <returns>True if decryption succeeded and payload has not expired; otherwise, false.</returns>
    public static bool TryUnprotectWithExpiry(
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
            unprotectedData = protector.UnprotectWithExpiry(protectedData);
            return true;
        }
        catch (CryptographicException)
        {
            unprotectedData = null;
            return false;
        }
        catch (Exception)
        {
            unprotectedData = null;
            return false;
        }
    }

    /// <summary>
    /// Re-encrypts an existing protected ciphertext with the currently active key in the keyring.
    /// </summary>
    /// <param name="protector">The data protector instance.</param>
    /// <param name="protectedData">The existing protected ciphertext.</param>
    /// <returns>The newly re-encrypted ciphertext under the active key.</returns>
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
    /// <param name="protector">The data protector instance.</param>
    /// <param name="protectedData">The existing ciphertext.</param>
    /// <param name="reEncryptedData">The re-encrypted ciphertext if successful.</param>
    /// <returns>True if re-encryption succeeded; otherwise, false.</returns>
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

    /// <summary>
    /// Creates a type-safe data protector based on the type's full name and optional sub-purpose.
    /// </summary>
    public static IDataProtector CreateProtector<T>(
        this IKyrolusDataProtectorFactory factory,
        string? subPurpose = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var basePurpose = typeof(T).FullName ?? typeof(T).Name;
        var fullPurpose = string.IsNullOrWhiteSpace(subPurpose)
            ? basePurpose
            : $"{basePurpose}.{subPurpose}";

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
        return provider.CreateProtector(tenantId, purpose);
    }
}
