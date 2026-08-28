

namespace KyrolusSous.Repositories.EF.Runtime;

public class KyrolusSingleKeySoftDeleteRepositoryAsync<TDbContext, TEntity, TKey> :
        KyrolusSingleKeyRepositoryAsync<TDbContext, TEntity, TKey>,
        IKyrolusSingleKeySoftDeleteRepository<TEntity, TKey>
        where TDbContext : DbContext
        where TEntity : class
        where TKey : IEquatable<TKey>
{

    public KyrolusSingleKeySoftDeleteRepositoryAsync(TDbContext db,
        KyrolusRepositoryPolicy? policy = null,
        IKyrolusRepositoryObserver? observer = null,
        IKyrolusBulkExecutor<TEntity>? bulkExecutor = null,
        IKyrolusCacheProvider? cache = null,
        bool enableCaching = false,
        int? cacheTtlSeconds = null,
        IKyrolusCacheKeyContext? cacheKeyContext = null,
        IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null,
        IKyrolusRepositoryPolicyProvider? policyProvider = null) :
        base(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds, cacheKeyContext, cachePolicyProvider, policyProvider)
    {
        softDeleteEnabled = true;
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
        => await GetAllInternalAsync(new GetAllCommand(nameof(GetAllIncludingDeletedAsync),
                    includeProperties is not { Count: > 0 } && includeGraph is not { Includes.Count: > 0 }
                    , filter, orderBy, includeProperties, includeGraph, asNoTracking, useSplitQuery, cancellationToken, false, true)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[]? includeExpressions)
        => await GetAllInternalAsync(new GetAllCommand(nameof(GetAllIncludingDeletedAsync),
                    includeExpressions is not { Length: > 0 }
                    , filter, orderBy, null, null, asNoTracking, useSplitQuery, cancellationToken, false, true, includeExpressions)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity?> GetByIdIncludingDeletedAsync(TKey id,
        bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    => await GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdIncludingDeletedAsync),
                    includeExpressions is not { Length: > 0 }, [id], null, null, asNoTracking, useSplitQuery, true, cancellationToken, includeExpressions)).ConfigureAwait(false);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity?> GetByIdIncludingDeletedAsync(TKey id, List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
      => await GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdIncludingDeletedAsync),
                    includeProperties is not { Count: > 0 } && includeGraph is not { Includes.Count: > 0 }, [id], includeProperties, includeGraph, asNoTracking, useSplitQuery, true, cancellationToken)).ConfigureAwait(false);
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
    public Task<bool> RestoreAsync(TKey id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return ExecuteWithNotificationsAsync(nameof(RestoreAsync), new { Id = id }, async ct =>
            await RestoreInternalAsync([id], ct).ConfigureAwait(false),
            kv => new { Id = id }, null, cancellationToken);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return ExecuteWithNotificationsAsync(nameof(SoftDeleteAsync), new { Id = id }, async ct =>
            await RemoveInternalAsync([id], true, ct).ConfigureAwait(false),
            kv => new { Id = id }, null, cancellationToken);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<bool>> TryRestoreAsync(TKey id, CancellationToken cancellationToken = default)
    => TryRestoreInternalAsync([id], nameof(TryRestoreAsync), cancellationToken);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<bool>> TrySoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    => TryRemoveInternalAsync([id], nameof(TrySoftDeleteAsync), true, cancellationToken);
}