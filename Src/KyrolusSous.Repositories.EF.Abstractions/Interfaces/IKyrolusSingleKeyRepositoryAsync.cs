namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

/// <summary>
/// Repository contract for entities with single.
/// </summary>
public interface IKyrolusSingleKeyRepositoryAsync<TDbContext, TEntity, TKey>
    where TEntity : class
    where TKey : IEquatable<TKey>
    where TDbContext : class
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdAsync(TKey id,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null,
        bool? useSplitQuery = null, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdAsync(TKey id,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdCompiledAsync(TKey id, CancellationToken cancellationToken = default);

    Task<TEntity?> PatchAsync(TKey id, Dictionary<string, object> updates, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<TEntity>> TryPatchAsync(TKey id, Dictionary<string, object> updates, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(TKey id, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<bool>> TryRemoveAsync(TKey id, CancellationToken cancellationToken = default);
}
