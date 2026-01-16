

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
        ICacheProvider? cache = null,
        bool enableCaching = false,
        int? cacheTtlSeconds = null,
        ICacheKeyContext? cacheKeyContext = null,
        IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null) :
        base(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds, cacheKeyContext, cachePolicyProvider)
    {
        base.softDeleteEnabled = true;
    }

    public async Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    {
        var GetAllIncludingDeletedAsync = "GetAllIncludingDeletedAsync";
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync(GetAllIncludingDeletedAsync, filter, cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetAllInternalAsync(new GetAllCommand(true, false, filter, orderBy, includeProperties, includeGraph, asNoTracking, useSplitQuery, cancellationToken));
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync(GetAllIncludingDeletedAsync, filter, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<TEntity?> GetByIdIncludingDeletedAsync(TKey id,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        var GetByIdIncludingDeletedAsync = "GetByIdIncludingDeletedAsync";
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync(GetByIdIncludingDeletedAsync, id, cancellationToken).ConfigureAwait(false);
        try
        {
            if (cache is not null && (includeExpressions == null || includeExpressions.Length == 0))
            {
                var cachePolicy = await ResolveCachePolicyAsync(GetByIdIncludingDeletedAsync, cancellationToken).ConfigureAwait(false);
                if (IsCacheEnabled(cachePolicy))
                {
                    var cacheKey = CacheKeyById([id], cachePolicy.KeySuffix) + ":incdel=1";
                    var options = BuildCacheEntryOptions(cachePolicy);
                    return await cache.GetOrCreateAsync(
                        cacheKey,
                        async ct => await MaterializeByIdAsync([id], asNoTracking, useSplitQuery, [], ct, true).ConfigureAwait(false),
                        options,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return await MaterializeByIdAsync([id], asNoTracking, useSplitQuery, includeExpressions, cancellationToken, true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync(GetByIdIncludingDeletedAsync, id, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<TEntity>> GetDeletedOnlyAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    {
        var GetDeletedOnlyAsync = "GetDeletedOnlyAsync";
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync(GetDeletedOnlyAsync, filter, cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetAllInternalAsync(new GetAllCommand(true, true, filter, orderBy, includeProperties, includeGraph, asNoTracking, useSplitQuery, cancellationToken));
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync(GetDeletedOnlyAsync, filter, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<bool> RestoreAsync(TKey id, CancellationToken cancellationToken = default)
    => RestoreInternalAsync([id], cancellationToken);


    public Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    => RemoveInternalAsync([id], true, cancellationToken);


    public Task<RepositoryOperationResult<bool>> TryRestoreAsync(TKey id, CancellationToken cancellationToken = default)
    => TryRestoreInternalAsync([id], cancellationToken);

    public Task<RepositoryOperationResult<bool>> TrySoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    => TryRemoveInternalAsync([id], true, cancellationToken);
}
