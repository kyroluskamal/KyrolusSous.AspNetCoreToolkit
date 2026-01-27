namespace KyrolusSous.Repositories.EF.Runtime;

/// <summary>
/// Runtime repository for single-key entities; thin wrapper over the common implementation.
/// </summary>
public class KyrolusSingleKeyRepositoryAsync<TDbContext, TEntity, TKey> :
    KyrolusRepositoryAsync<TDbContext, TEntity, TKey>,
    IKyrolusSingleKeyRepositoryAsync<TDbContext, TEntity, TKey>
    where TDbContext : DbContext
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    public KyrolusSingleKeyRepositoryAsync(
        TDbContext db,
        KyrolusRepositoryPolicy? policy = null,
        IKyrolusRepositoryObserver? observer = null,
        IKyrolusBulkExecutor<TEntity>? bulkExecutor = null,
        ICacheProvider? cache = null,
        bool enableCaching = false,
        int? cacheTtlSeconds = null,
        ICacheKeyContext? cacheKeyContext = null,
        IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null,
        IKyrolusRepositoryPolicyProvider? policyProvider = null)
        : base(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds, cacheKeyContext, cachePolicyProvider, policyProvider)
    {
        base.softDeleteEnabled = false;
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(TKey id,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    => GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdAsync), includeProperties is not { Count: > 0 } && includeGraph is not { Includes.Count: > 0 }, [id], includeProperties, includeGraph, asNoTracking, useSplitQuery, false, cancellationToken));

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(TKey id, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
        => GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdAsync), true, [id], null, null, asNoTracking, useSplitQuery, false, cancellationToken, includeExpressions));

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity?> GetByIdCompiledAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithNotificationsAsync(nameof(GetByIdCompiledAsync), id, async ct =>
            {
                ArgumentNullException.ThrowIfNull(id);
                if (globalQueryFilter is not null || keyPropertyNames.Length != 1)
                    return await GetByIdInternalAsync(new GetByIdCommand(nameof(GetByIdCompiledAsync), true, [id], null, null, false, false, false, cancellationToken)).ConfigureAwait(false);

                var del = CompiledById.GetOrAdd(typeof(TEntity), _ =>
                {
                    var keyName = keyPropertyNames.FirstOrDefault() ?? throw new InvalidOperationException("Primary key not found.");
                    return Microsoft.EntityFrameworkCore.EF.CompileAsyncQuery((TDbContext ctx, TKey key) =>
                        ctx.Set<TEntity>().Where(e => Microsoft.EntityFrameworkCore.EF.Property<TKey>(e, keyName)!.Equals(key)));
                });
                var cachePolicy = await ResolveCachePolicyAsync(nameof(GetByIdCompiledAsync), cancellationToken).ConfigureAwait(false);
                if (cache is not null && IsReadCacheAllowed(nameof(GetByIdCompiledAsync), cachePolicy))
                {
                    var cacheKey = CacheKeyById(nameof(GetByIdCompiledAsync), [id], cachePolicy.KeySuffix);
                    var options = BuildCacheEntryOptions(cachePolicy);
                    return await cache.GetOrCreateAsync(
                        cacheKey,
                        async ct => await del(db, id).FirstOrDefaultAsync(ct).ConfigureAwait(false),
                        options,
                        cancellationToken).ConfigureAwait(false);

                }
                return await del(db, id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            },
            e => new { Id = id }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity?> PatchAsync(TKey id, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(updates);
        return await ExecuteWithNotificationsAsync(nameof(PatchAsync), (id, updates), async ct =>
            await PatchInternalAsync([id], updates, cancellationToken).ConfigureAwait(false),
            e => new { Id = id, Updates = updates }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<TEntity>> TryPatchAsync(TKey id, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    => TryPatchInternalAsync([id], nameof(TryPatchAsync), updates, cancellationToken);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<bool> RemoveAsync(TKey id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await ExecuteWithNotificationsAsync(nameof(RemoveAsync), new { Id = id }, async ct =>
            await RemoveInternalAsync([id], false, ct).ConfigureAwait(false),
            kv => new { Id = id }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<bool>> TryRemoveAsync(TKey id, CancellationToken cancellationToken = default)
        => TryRemoveInternalAsync([id], nameof(TryRemoveAsync), false, cancellationToken);
}
