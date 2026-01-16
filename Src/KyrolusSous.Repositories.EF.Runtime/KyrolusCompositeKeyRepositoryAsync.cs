

using KyrolusSous.Caching.Abstractions;

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
    IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null) :
    KyrolusRepositoryAsync<TDbContext, TEntity, object?>(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds, cacheKeyContext, cachePolicyProvider),
    IKyrolusCompositeKeyRepositoryAsync<TDbContext, TEntity, object?>
    where TDbContext : DbContext
    where TEntity : class
{

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(object?[] keyValues,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    {
        var hasIncludeProps = includeProperties is { Count: > 0 } && includeProperties.Any(p => !string.IsNullOrWhiteSpace(p));
        if (!hasIncludeProps)
        {
            var includes = includeGraph?.Includes?.ToArray() ?? [];
            return GetByIdInternalAsync(keyValues ?? [], asNoTracking, useSplitQuery,false, cancellationToken,  includes);
        }
        return GetByIdInternalWithStringIncludesAsync(keyValues ?? [], includeProperties!, includeGraph, asNoTracking, useSplitQuery, cancellationToken);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(object?[] keyValues,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        return GetByIdInternalAsync(keyValues ?? [], asNoTracking, useSplitQuery,false, cancellationToken,  includeExpressions);
    }

    public Task<TEntity?> PatchAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
        => PatchInternalAsync(keyValues, updates, cancellationToken);

    public Task<RepositoryOperationResult<TEntity>> TryPatchAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
        => TryPatchInternalAsync(keyValues, updates, cancellationToken);

    public Task<bool> RemoveAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
        => RemoveInternalAsync(keyValues, false, cancellationToken);

    public Task<RepositoryOperationResult<bool>> TryRemoveAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
        => TryRemoveInternalAsync(keyValues, false, cancellationToken);
}
