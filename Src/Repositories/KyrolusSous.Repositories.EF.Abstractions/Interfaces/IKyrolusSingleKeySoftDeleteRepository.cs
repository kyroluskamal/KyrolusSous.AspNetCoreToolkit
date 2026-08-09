namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

/// <summary>
/// Soft-delete contract for single key entity.
/// </summary>
public interface IKyrolusSingleKeySoftDeleteRepository<TEntity, TKey> : IKyrolusSoftDeleteRepository<TEntity>
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<bool>> TrySoftDeleteAsync(TKey id, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<bool>> TryRestoreAsync(TKey id, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> RestoreAsync(TKey id, CancellationToken cancellationToken = default);


    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdIncludingDeletedAsync(TKey id,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdIncludingDeletedAsync(TKey id,
         List<string>? includeProperties = null,
        IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IReadOnlyList<TEntity>> GetDeletedOnlyAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetDeletedOnlyAsync(Expression<Func<TEntity, bool>>? filter = null,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool? asNoTracking = null, bool? useSplitQuery = null,
        CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions);
}
