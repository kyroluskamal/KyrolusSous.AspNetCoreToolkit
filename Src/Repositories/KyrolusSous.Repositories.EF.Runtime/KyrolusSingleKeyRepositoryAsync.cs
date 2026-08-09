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
                    return await GetByIdAsync(id, cancellationToken: ct).ConfigureAwait(false);

                var keyName = keyPropertyNames[0];
                var useSoftDelete = softDeleteEnabled && !string.IsNullOrWhiteSpace(softDeleteProperty);
                var defaultIncludeProperties = policyDefaultIncludeProperties;
                var defaultIncludesKey = defaultIncludeProperties.Length == 0 ? string.Empty : string.Join("|", defaultIncludeProperties);
                var requestedNoTracking = asNoTrackingDefault;
                var requestedSplit = splitQueryDefault;

                var compiledKey = (typeof(TEntity), useSoftDelete, softDeleteProperty, defaultIncludesKey, requestedNoTracking, requestedSplit, keyName);
                var del = CompiledById.GetOrAdd(compiledKey, _ =>
                    BuildCompiledById(
                        useSoftDelete,
                        softDeleteProperty,
                        defaultIncludeProperties,
                        requestedNoTracking,
                        requestedSplit,
                        keyName));

                var cachePolicy = await ResolveCachePolicyAsync(nameof(GetByIdCompiledAsync), ct).ConfigureAwait(false);
                if (cache is not null && IsReadCacheAllowed(nameof(GetByIdCompiledAsync), cachePolicy))
                {
                    var cacheKey = CacheKeyById(nameof(GetByIdCompiledAsync), [id], cachePolicy.KeySuffix);
                    var options = BuildCacheEntryOptions(cachePolicy);
                    return await cache.GetOrCreateAsync(
                        cacheKey,
                        async innerCt => await del(db, id).FirstOrDefaultAsync(innerCt).ConfigureAwait(false),
                        options,
                        ct).ConfigureAwait(false);

                }
                return await del(db, id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            },
            e => new { Id = id }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    private static Func<TDbContext, TKey, IAsyncEnumerable<TEntity>> BuildCompiledById(
        bool useSoftDelete,
        string softDeleteProperty,
        string[] defaultIncludeProperties,
        bool asNoTracking,
        bool useSplitQuery,
        string keyName)
    {
        var ctxParam = Expression.Parameter(typeof(TDbContext), "ctx");
        var keyParam = Expression.Parameter(typeof(TKey), "key");

        var setMethod = typeof(DbContext).GetMethods();
        var setGeneric = setMethod.Single(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethod && m.GetParameters().Length == 0)
            .MakeGenericMethod(typeof(TEntity));
        Expression query = Expression.Call(ctxParam, setGeneric);

        if (useSoftDelete)
        {
            var entityParam = Expression.Parameter(typeof(TEntity), "e");
            var efBoolPropertyMethod = typeof(Microsoft.EntityFrameworkCore.EF).GetMethod(nameof(Microsoft.EntityFrameworkCore.EF.Property))!
                .MakeGenericMethod(typeof(bool));
            var softDeleteAccess = Expression.Call(efBoolPropertyMethod, entityParam, Expression.Constant(softDeleteProperty));
            var softDeletePredicate = Expression.Lambda<Func<TEntity, bool>>(Expression.Not(softDeleteAccess), entityParam);
            var whereMethod = GetQueryableWhereMethod().MakeGenericMethod(typeof(TEntity));
            query = Expression.Call(whereMethod, query, Expression.Quote(softDeletePredicate));
        }

        if (defaultIncludeProperties.Length > 0)
        {
            var includeStringMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType == typeof(string))
                .MakeGenericMethod(typeof(TEntity));

            foreach (var includeProperty in defaultIncludeProperties)
            {
                if (string.IsNullOrWhiteSpace(includeProperty)) continue;
                query = Expression.Call(includeStringMethod, query, Expression.Constant(includeProperty));
            }
        }

        var keyEntityParam = Expression.Parameter(typeof(TEntity), "e");
        var efKeyPropertyMethod = typeof(Microsoft.EntityFrameworkCore.EF).GetMethod(nameof(Microsoft.EntityFrameworkCore.EF.Property))!
            .MakeGenericMethod(typeof(TKey));
        var keyAccess = Expression.Call(efKeyPropertyMethod, keyEntityParam, Expression.Constant(keyName));
        var keyPredicate = Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(keyAccess, keyParam), keyEntityParam);

        var keyWhereMethod = GetQueryableWhereMethod().MakeGenericMethod(typeof(TEntity));
        query = Expression.Call(keyWhereMethod, query, Expression.Quote(keyPredicate));

        var asQueryable = query.Type == typeof(IQueryable<TEntity>)
            ? query
            : Expression.Convert(query, typeof(IQueryable<TEntity>));

        if (asNoTracking)
        {
            var asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking) && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(TEntity));
            asQueryable = Expression.Call(asNoTrackingMethod, asQueryable);
        }

        if (useSplitQuery)
        {
            var asSplitMethod = typeof(RelationalQueryableExtensions).GetMethods()
                .Single(m => m.Name == nameof(RelationalQueryableExtensions.AsSplitQuery) && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(TEntity));
            asQueryable = Expression.Call(asSplitMethod, asQueryable);
        }

        var lambda = Expression.Lambda<Func<TDbContext, TKey, IQueryable<TEntity>>>(
            asQueryable,
            ctxParam,
            keyParam);
        return Microsoft.EntityFrameworkCore.EF.CompileAsyncQuery(lambda);
    }

    private static System.Reflection.MethodInfo GetQueryableWhereMethod()
        => typeof(Queryable).GetMethods()
            .Single(m => m.Name == nameof(Queryable.Where)
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.IsGenericType
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].IsGenericType
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>));
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
