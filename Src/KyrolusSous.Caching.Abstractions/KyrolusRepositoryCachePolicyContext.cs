namespace KyrolusSous.Caching.Abstractions;

public sealed record KyrolusRepositoryCachePolicyContext(
    Type EntityType,
    string? RepositoryName = null,
    string? Operation = null,
    string? ScopeKey = null,
    string? TenantId = null);
