

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
        int? cacheTtlSeconds = null) :
        base(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds)
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
            if (enableCaching && cache is not null && cacheTtl.HasValue && (includeExpressions == null || includeExpressions.Length == 0))
            {
                var cacheKey = CacheKeyById([id]) + ":incdel=1";
                return await cache.GetOrSetAsync(
                    cacheKey,
                    async ct => await MaterializeByIdAsync([id], asNoTracking, useSplitQuery, [], ct, true).ConfigureAwait(false),
                    cacheTtl,
                    cancellationToken).ConfigureAwait(false);
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
