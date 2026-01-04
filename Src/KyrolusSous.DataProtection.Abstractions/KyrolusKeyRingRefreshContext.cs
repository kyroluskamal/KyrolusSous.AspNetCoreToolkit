namespace KyrolusSous.DataProtection.Abstractions;

public sealed record KyrolusKeyRingRefreshContext(
    DateTimeOffset RefreshedAt,
    IReadOnlyList<KyrolusDataProtectionKeyInfo>? Keys);
