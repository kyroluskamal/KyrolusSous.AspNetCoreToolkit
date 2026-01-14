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
    int? cacheTtlSeconds = null) : base(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds)
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

    public async Task<TEntity?> GetByIdIncludingDeletedAsync(object?[]? keyValues, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        var GetByIdIncludingDeletedAsync = "GetByIdIncludingDeletedAsync";
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync(GetByIdIncludingDeletedAsync, keyValues, cancellationToken).ConfigureAwait(false);
        try
        {
            if (enableCaching && cache is not null && cacheTtl.HasValue && (includeExpressions == null || includeExpressions.Length == 0))
            {
                var cacheKey = CacheKeyById(keyValues!) + ":incdel=1";
                return await cache.GetOrSetAsync(
                    cacheKey,
                    async ct => await MaterializeByIdAsync(keyValues!, asNoTracking, useSplitQuery, [], ct, true).ConfigureAwait(false),
                    cacheTtl,
                    cancellationToken).ConfigureAwait(false);
            }

            return await MaterializeByIdAsync(keyValues!, asNoTracking, useSplitQuery, includeExpressions, cancellationToken, true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync(GetByIdIncludingDeletedAsync, keyValues, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
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

    public Task<bool> RestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => RestoreInternalAsync(keyValues, cancellationToken);
    public Task<bool> SoftDeleteAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => RemoveInternalAsync(keyValues, true, cancellationToken);
    public Task<RepositoryOperationResult<bool>> TryRestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => TryRestoreInternalAsync(keyValues, cancellationToken);

    public Task<RepositoryOperationResult<bool>> TrySoftDeleteAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    => TryRemoveInternalAsync(keyValues, true, cancellationToken);
}
