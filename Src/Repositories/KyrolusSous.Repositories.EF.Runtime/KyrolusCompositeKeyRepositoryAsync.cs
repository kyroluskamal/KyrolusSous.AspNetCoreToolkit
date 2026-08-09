
namespace KyrolusSous.Repositories.EF.Runtime;

/// <summary>
/// Runtime repository for composite-key entities; thin wrapper over the common implementation using object keys.
/// </summary>
public class KyrolusCompositeKeyRepositoryAsync<TDbContext, TEntity>(
    TDbContext db,
    KyrolusRepositoryPolicy? policy = null,
    IKyrolusRepositoryObserver? observer = null,
    IKyrolusBulkExecutor<TEntity>? bulkExecutor = null,
    ICacheProvider? cache = null,
    bool enableCaching = false,
    int? cacheTtlSeconds = null,
    ICacheKeyContext? cacheKeyContext = null,
    IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null,
    IKyrolusRepositoryPolicyProvider? policyProvider = null) :
    KyrolusRepositoryAsync<TDbContext, TEntity, object?>(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds, cacheKeyContext, cachePolicyProvider, policyProvider),
    IKyrolusCompositeKeyRepositoryAsync<TDbContext, TEntity, object?>
    where TDbContext : DbContext
    where TEntity : class
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(object?[] keyValues,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    => GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdAsync), 
    includeProperties is not { Count: > 0 } && includeGraph is not { Includes.Count: > 0 },
    keyValues, 
    includeProperties, 
    includeGraph, 
    asNoTracking, 
    useSplitQuery, 
    false, 
    cancellationToken));
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(object?[] keyValues, bool? asNoTracking = null,
        bool? useSplitQuery = null, CancellationToken cancellationToken = default,params Expression<Func<TEntity, object?>>[] includeExpressions)
        => GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdAsync), true, keyValues, null, null, asNoTracking, useSplitQuery, false, cancellationToken, includeExpressions));
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity?> PatchAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfKeyValuesIsNotValid(keyValues, keyPropertyNames.Length);
        ArgumentException.ThrowIfUpdatesIsNotValid(updates);
        return await ExecuteWithNotificationsAsync(nameof(PatchAsync), (keyValues, updates), async ct =>
            await PatchInternalAsync(keyValues, updates, cancellationToken).ConfigureAwait(false),
            e => new { KeyValues = keyValues, Updates = updates }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<TEntity>> TryPatchAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    => TryPatchInternalAsync(keyValues, nameof(TryPatchAsync), updates, cancellationToken);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<bool> RemoveAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfKeyValuesIsNotValid(keyValues, keyPropertyNames.Length);
        return ExecuteWithNotificationsAsync(nameof(RemoveAsync), keyValues, async ct =>
            await RemoveInternalAsync(keyValues, false, ct).ConfigureAwait(false),
            kv => new { KeyValues = kv }, ex => new { Exception = ex.Message }, cancellationToken);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<bool>> TryRemoveAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
        => TryRemoveInternalAsync(keyValues, nameof(TryRemoveAsync), false, cancellationToken);
}
