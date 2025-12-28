using System.Collections.Generic;
using System.Linq.Expressions;
using System.Diagnostics.CodeAnalysis;
using KyrolusSous.Repositories.EF.Abstractions;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Policy;
using Microsoft.EntityFrameworkCore;

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
    int? cacheTtlSeconds = null) :
    KyrolusRepositoryAsync<TDbContext, TEntity, object?>(db, policy, observer, bulkExecutor, cache, enableCaching, cacheTtlSeconds),
    IKyrolusCompositeKeyRepositoryAsync<TDbContext, TEntity, object?>,
    IKyrolusCompositeKeySoftDeleteRepository<TEntity>
    where TDbContext : DbContext
    where TEntity : class
{

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(object?[] keyValues,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    {
        var includes = BuildIncludes(includeProperties, includeGraph);
        return GetByIdInternalAsync(keyValues ?? [], asNoTracking, useSplitQuery, cancellationToken, includes);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<TEntity?> GetByIdAsync(object?[] keyValues,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        return GetByIdInternalAsync(keyValues ?? [], asNoTracking, useSplitQuery, cancellationToken, includeExpressions);
    }

    public Task<TEntity?> PatchAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
        => PatchInternalAsync(keyValues, updates, cancellationToken);

    public Task<RepositoryOperationResult<TEntity>> TryPatchAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
        => TryPatchInternalAsync(keyValues, updates, cancellationToken);

    public Task<bool> RemoveAsync(object?[]? keyValues, bool isSoftDelete = true, CancellationToken cancellationToken = default)
        => RemoveInternalAsync(keyValues, isSoftDelete, cancellationToken);

    public Task<RepositoryOperationResult<bool>> TryRemoveAsync(object?[]? keyValues, bool isSoftDelete, CancellationToken cancellationToken = default)
        => TryRemoveInternalAsync(keyValues, isSoftDelete, cancellationToken);

    public Task<bool> RestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
        => RestoreInternalAsync(keyValues, cancellationToken);

    public Task<RepositoryOperationResult<bool>> TryRestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
        => TryRestoreInternalAsync(keyValues, cancellationToken);
}
