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
    IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null) : base(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds, cacheKeyContext, cachePolicyProvider)
    {
        softDeleteEnabled = true;
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

    public async Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        var GetAllIncludingDeletedAsync = "GetAllIncludingDeletedAsync";
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync(GetAllIncludingDeletedAsync, filter, cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetAllInternalAsync(new GetAllCommand(true, false, filter, orderBy, null, null, asNoTracking, useSplitQuery, cancellationToken, includeExpressions));
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

    public async Task<TEntity?> GetByIdIncludingDeletedAsync(object?[]? keyValues, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync(nameof(GetByIdIncludingDeletedAsync), keyValues, cancellationToken).ConfigureAwait(false);
        try
        {
            ArgumentNullException.ThrowIfNull(keyValues);

            if (cache is not null && (includeExpressions == null || includeExpressions.Length == 0))
            {
                var cachePolicy = await ResolveCachePolicyAsync(nameof(GetByIdIncludingDeletedAsync), cancellationToken).ConfigureAwait(false);
                if (IsCacheEnabled(cachePolicy))
                {
                    var cacheKey = CacheKeyById(keyValues!, cachePolicy.KeySuffix) + ":incdel=1";
                    var options = BuildCacheEntryOptions(cachePolicy);
                    return await cache.GetOrCreateAsync(
                        cacheKey,
                        async ct => await MaterializeByIdAsync(new MaterializeByIdCommand(keyValues, true, null, null, includeExpressions ?? [], asNoTracking, useSplitQuery, ct)).ConfigureAwait(false),
                        options,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return await MaterializeByIdAsync(new MaterializeByIdCommand(keyValues, true, null, null, includeExpressions ?? [], asNoTracking, useSplitQuery, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync(nameof(GetByIdIncludingDeletedAsync), keyValues, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<TEntity?> GetByIdIncludingDeletedAsync(object?[]? keyValues, List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyValues);

        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync(nameof(GetByIdIncludingDeletedAsync), keyValues, cancellationToken).ConfigureAwait(false);
        try
        {
            if (cache is not null && (includeProperties is not { Count: > 0 } || includeGraph is not { Includes.Count: > 0 }))
            {
                var cachePolicy = await ResolveCachePolicyAsync(nameof(GetByIdIncludingDeletedAsync), cancellationToken).ConfigureAwait(false);
                if (IsCacheEnabled(cachePolicy))
                {
                    var cacheKey = CacheKeyById(keyValues!, cachePolicy.KeySuffix) + ":incdel=1";
                    var options = BuildCacheEntryOptions(cachePolicy);
                    return await cache.GetOrCreateAsync(
                        cacheKey,
                        async ct => await MaterializeByIdAsync(new MaterializeByIdCommand(keyValues, true, includeProperties, includeGraph, [], asNoTracking, useSplitQuery, ct)).ConfigureAwait(false),
                        options,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return await MaterializeByIdAsync(new MaterializeByIdCommand(keyValues, true, includeProperties, includeGraph, [], asNoTracking, useSplitQuery, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync(nameof(GetByIdIncludingDeletedAsync), keyValues, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
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

    public Task<IReadOnlyList<TEntity>> GetDeletedOnlyAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        throw new NotImplementedException();
    }

    public Task<bool> RestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => RestoreInternalAsync(keyValues, cancellationToken);
    public Task<bool> SoftDeleteAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => RemoveInternalAsync(keyValues, true, cancellationToken);
    public Task<RepositoryOperationResult<bool>> TryRestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => TryRestoreInternalAsync(keyValues, cancellationToken);

    public Task<RepositoryOperationResult<bool>> TrySoftDeleteAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => TryRemoveInternalAsync(keyValues, true, cancellationToken);
}
