namespace KyrolusSous.DataProtection.Abstractions;

public sealed record KyrolusDataProtectionKeyInfo(
    Guid KeyId,
    DateTimeOffset ActivationDate,
    DateTimeOffset ExpirationDate,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? RevokedAt,
    bool IsRevoked);
