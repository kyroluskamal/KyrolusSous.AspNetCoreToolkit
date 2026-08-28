using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// A secure payload transformer that encrypts and decrypts cache byte payloads using ASP.NET Core Data Protection Key Ring.
/// Supports automated key rotation, cryptographic agility, and multi-node protection (Redis, Azure Key Vault, AWS KMS, Vault).
/// </summary>
public sealed class KyrolusDataProtectionCachePayloadTransformer : IKyrolusCachePayloadTransformer
{
    private const string DefaultPurpose = "KyrolusSous.Caching.PayloadProtection";
    private readonly IDataProtector _protector;

    public KyrolusDataProtectionCachePayloadTransformer(IDataProtectionProvider dataProtectionProvider, string purpose = DefaultPurpose)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _protector = dataProtectionProvider.CreateProtector(string.IsNullOrWhiteSpace(purpose) ? DefaultPurpose : purpose);
    }

    public KyrolusDataProtectionCachePayloadTransformer(IDataProtector protector)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public byte[] Transform(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length == 0)
        {
            return [];
        }

        return _protector.Protect(payload);
    }

    public byte[] Restore(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length == 0)
        {
            return [];
        }

        return _protector.Unprotect(payload);
    }
}
