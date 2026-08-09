namespace KyrolusSous.DataProtection.Abstractions;

public interface IKyrolusDataProtectionKeyManager
{
    Task<IReadOnlyList<KyrolusDataProtectionKeyInfo>> GetAllKeysAsync(CancellationToken cancellationToken = default);
    Task<KyrolusDataProtectionKeyInfo?> GetKeyAsync(Guid keyId, CancellationToken cancellationToken = default);
    Task<KyrolusDataProtectionKeyInfo> CreateKeyAsync(
        DateTimeOffset? activationDate = null,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default);
    Task<KyrolusDataProtectionKeyInfo> RotateKeyAsync(TimeSpan? lifetime = null, CancellationToken cancellationToken = default);
    Task RevokeKeyAsync(Guid keyId, string? reason = null, CancellationToken cancellationToken = default);
    Task RevokeAllKeysAsync(
        DateTimeOffset? revocationDate = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
