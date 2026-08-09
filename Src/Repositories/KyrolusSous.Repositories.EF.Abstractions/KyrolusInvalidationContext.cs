namespace KyrolusSous.Repositories.EF.Abstractions;

public readonly record struct KyrolusInvalidationContext(
    string Entity,
    string? Tenant,
    string? Scope,
    string? PolicySuffix,
    string? KeyFingerprint,
    string AllKey,
    string AllCompiledKey,
    string? IdKey,
    string? IdCompiledKey);