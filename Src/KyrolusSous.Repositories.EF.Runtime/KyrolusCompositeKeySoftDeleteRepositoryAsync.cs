namespace KyrolusSous.Repositories.EF.Runtime;

public class KyrolusCompositeKeySoftDeleteRepositoryAsync<TDbContext, TEntity> :
    KyrolusRepositoryAsync<TDbContext, TEntity, object?>,
    IKyrolusCompositeKeySoftDeleteRepository<TEntity>
    where TDbContext : DbContext
    where TEntity : class
{
    public KyrolusCompositeKeySoftDeleteRepositoryAsync(
    TDbContext db,
    KyrolusRepositoryPolicy? policy = null,
    IKyrolusRepositoryObserver? observer = null,
    IKyrolusBulkExecutor<TEntity>? bulkExecutor = null,
    ICacheProvider? cache = null,
    bool enableCaching = false,
    int? cacheTtlSeconds = null,
    ICacheKeyContext? cacheKeyContext = null,
    IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null,
    IKyrolusRepositoryPolicyProvider? policyProvider = null) : base(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds, cacheKeyContext, cachePolicyProvider, policyProvider)
    {
        softDeleteEnabled = true;
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    => await GetAllInternalAsync(new GetAllCommand(nameof(GetAllIncludingDeletedAsync),
                    includeProperties is not { Count: > 0 } && includeGraph is not { Includes.Count: > 0 }
                    , filter, orderBy, includeProperties, includeGraph, asNoTracking, useSplitQuery, cancellationToken, false, true)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions)
    => await GetAllInternalAsync(new GetAllCommand(nameof(GetAllIncludingDeletedAsync),
                    includeExpressions is not { Length: > 0 }
                    , filter, orderBy, null, null, asNoTracking, useSplitQuery, cancellationToken, false, true, includeExpressions)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity?> GetByIdIncludingDeletedAsync(object?[]? keyValues, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions)
    => await GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdIncludingDeletedAsync),
                    includeExpressions is not { Length: > 0 }, keyValues, null, null, asNoTracking, useSplitQuery, true, cancellationToken, includeExpressions)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity?> GetByIdIncludingDeletedAsync(object?[]? keyValues, List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
        => await GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdIncludingDeletedAsync),
                    includeProperties is not { Count: > 0 } && includeGraph is not { Includes.Count: > 0 }, keyValues, includeProperties, includeGraph, asNoTracking, useSplitQuery, true, cancellationToken)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IReadOnlyList<TEntity>> GetDeletedOnlyAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    => await GetAllInternalAsync(new GetAllCommand(nameof(GetDeletedOnlyAsync),
                    includeProperties is not { Count: > 0 } && includeGraph is not { Includes.Count: > 0 }
                    , filter, orderBy, includeProperties, includeGraph, asNoTracking, useSplitQuery, cancellationToken, true, true)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IReadOnlyList<TEntity>> GetDeletedOnlyAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions)
     => await GetAllInternalAsync(new GetAllCommand(nameof(GetDeletedOnlyAsync),
                    includeExpressions is not { Length: > 0 }
                    , filter, orderBy, null, null, asNoTracking, useSplitQuery, cancellationToken, true, true, includeExpressions)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<bool> RestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfKeyValuesIsNotValid(keyValues, keyPropertyNames.Length);
        return ExecuteWithNotificationsAsync(nameof(RestoreAsync), keyValues, async ct =>
            await RestoreInternalAsync(keyValues, ct).ConfigureAwait(false),
            kv => new { KeyValues = kv }, ex => new { Exception = ex.Message }, cancellationToken);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<bool> SoftDeleteAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfKeyValuesIsNotValid(keyValues, keyPropertyNames.Length);
        return ExecuteWithNotificationsAsync(nameof(SoftDeleteAsync), keyValues, async ct =>
            await RemoveInternalAsync(keyValues, true, ct).ConfigureAwait(false),
            kv => new { KeyValues = kv }, ex => new { Exception = ex.Message }, cancellationToken);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<bool>> TryRestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => TryRestoreInternalAsync(keyValues, nameof(TryRestoreAsync), cancellationToken);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<bool>> TrySoftDeleteAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => TryRemoveInternalAsync(keyValues, nameof(TrySoftDeleteAsync), true, cancellationToken);
}
