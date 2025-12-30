using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions.Records;

public sealed record MartenQueryOptions<TEntity>(
    Expression<Func<TEntity, bool>>? Filter = null,
    IQuerySpecification<TEntity>? Specification = null,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy = null,
    Action<IMartenQueryable<TEntity>>? ConfigureQuery = null,
    List<string>? IncludeProperties = null,
    Expression<Func<TEntity, object?>>[]? IncludeExpressions = null,
    string? TenantId = null,
    bool IncludeSoftDeleted = false);
