namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Encapsulates contextual metadata regarding a repository operation on an entity type 
/// used by <see cref="IKyrolusRepositoryCachePolicyProvider"/> to determine caching rules.
/// </summary>
/// <param name="EntityType">The CLR type of the database entity (e.g. <c>typeof(Product)</c>).</param>
/// <param name="RepositoryName">The logical or interface name of the repository executing the operation.</param>
/// <param name="Operation">The repository method or action being executed (e.g. <c>"GetById"</c>, <c>"ListAsync"</c>, <c>"AddAsync"</c>).</param>
/// <param name="ScopeKey">Optional composite scope isolation key.</param>
/// <param name="TenantId">Optional tenant identifier.</param>
public sealed record KyrolusRepositoryCachePolicyContext(
    Type EntityType,
    string? RepositoryName = null,
    string? Operation = null,
    string? ScopeKey = null,
    string? TenantId = null);
