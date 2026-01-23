namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

/// <summary>
/// Soft-delete contract for Composite key entities.
/// </summary>
public interface IKyrolusCompositeKeySoftDeleteRepository<TEntity>
    where TEntity : class
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<bool>> TrySoftDeleteAsync(object?[]? keyValues, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> SoftDeleteAsync(object?[]? keyValues, CancellationToken cancellationToken = default);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<bool>> TryRestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> RestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdIncludingDeletedAsync(object?[]? keyValues,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdIncludingDeletedAsync(object?[]? keyValues,
         List<string>? includeProperties = null,
        IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        List<string>? includeProperties = null,
        IncludeGraph<TEntity>? includeGraph = null,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IReadOnlyList<TEntity>> GetDeletedOnlyAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetDeletedOnlyAsync(Expression<Func<TEntity, bool>>? filter = null,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool? asNoTracking = null, bool? useSplitQuery = null,
        CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions);
}
