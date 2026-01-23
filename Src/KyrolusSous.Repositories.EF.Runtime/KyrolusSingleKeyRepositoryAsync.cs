using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Policy;
using KyrolusSous.Repositories.EF.Abstractions;
using Microsoft.EntityFrameworkCore;

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
        IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null)
        : base(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds, cacheKeyContext, cachePolicyProvider)
    {
        base.softDeleteEnabled = false;
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(TKey id,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    {
        var hasIncludeProps = includeProperties is { Count: > 0 } && includeProperties.Any(p => !string.IsNullOrWhiteSpace(p));
        if (!hasIncludeProps)
        {
            return GetByIdInternalAsync(new GetByIdCommand(
                [id],
                includeProperties,
                includeGraph,
                asNoTracking,
                useSplitQuery, false, cancellationToken));
        }
        return GetByIdInternalWithStringIncludesAsync([id], includeProperties!, includeGraph, asNoTracking, useSplitQuery, cancellationToken);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(TKey id,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        return GetByIdInternalAsync(new GetByIdCommand([id], null, null, asNoTracking, useSplitQuery, false, cancellationToken));
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity?> GetByIdCompiledAsync(TKey id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (globalQueryFilter is not null || keyPropertyNames.Length != 1)
            return await GetByIdInternalAsync(new GetByIdCommand([id], null, null, false, false, false, cancellationToken)).ConfigureAwait(false);

        var del = CompiledById.GetOrAdd(typeof(TEntity), _ =>
        {
            var keyName = keyPropertyNames.FirstOrDefault() ?? throw new InvalidOperationException("Primary key not found.");
            return Microsoft.EntityFrameworkCore.EF.CompileAsyncQuery((TDbContext ctx, TKey key) =>
                ctx.Set<TEntity>().Where(e => Microsoft.EntityFrameworkCore.EF.Property<TKey>(e, keyName)!.Equals(key)));
        });

        Exception? exception = null;
        var sw = Stopwatch.StartNew();
        await NotifyBeforeAsync("GetByIdCompiledAsync", id, cancellationToken).ConfigureAwait(false);
        try
        {
            if (cache is not null)
            {
                var cachePolicy = await ResolveCachePolicyAsync("GetByIdAsync", cancellationToken).ConfigureAwait(false);
                if (IsCacheEnabled(cachePolicy))
                {
                    var cacheKey = CacheKeyById([id], cachePolicy.KeySuffix);
                    var options = BuildCacheEntryOptions(cachePolicy);
                    return await cache.GetOrCreateAsync(
                        cacheKey,
                        async ct => await del(db, id).FirstOrDefaultAsync(ct).ConfigureAwait(false),
                        options,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            return await del(db, id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("GetByIdCompiledAsync", id, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<TEntity?> PatchAsync(TKey id, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
        => PatchInternalAsync([id], updates, cancellationToken);

    public Task<RepositoryOperationResult<TEntity>> TryPatchAsync(TKey id, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
        => TryPatchInternalAsync([id], updates, cancellationToken);

    public Task<bool> RemoveAsync(TKey id, CancellationToken cancellationToken = default)
        => RemoveInternalAsync([id], false, cancellationToken);

    public Task<RepositoryOperationResult<bool>> TryRemoveAsync(TKey id, CancellationToken cancellationToken = default)
        => TryRemoveInternalAsync([id], false, cancellationToken);
}
