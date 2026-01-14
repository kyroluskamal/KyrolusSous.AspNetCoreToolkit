namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusCompositeKeyRepositoryAsync<TDbContext, TEntity, TKey>
    where TEntity : class
    where TDbContext : class
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdAsync(object?[] keyValues,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity?> GetByIdAsync(object?[] keyValues,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions);

    Task<TEntity?> PatchAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<TEntity>> TryPatchAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(object?[]? keyValues, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<bool>> TryRemoveAsync(object?[]? keyValues, CancellationToken cancellationToken = default);

}
