namespace KyrolusSous.Repositories.Marten.Runtime.Repository.Decorators;

/// <summary>
/// Generic decorator that wires resilience/tracing/authorization/validation/cache/observer around an inner repository.
/// Logic kept minimal; core behavior delegated to inner repository.
/// </summary>
public class KyrolusMartenRepositoryDecorator<TSession, TEntity, TKey> : IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey>
    where TSession : IDocumentSession
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    private readonly IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey> inner;
    private readonly IKyrolusMartenCacheProvider? cache;
    private readonly IKyrolusMartenResiliencePolicy? resilience;
    private readonly IKyrolusMartenTracing? tracing;
    public IKyrolusMartenObserver? Observer => inner.Observer;
    public IKyrolusMartenAuthorization? Authorization => inner.Authorization;
    public IKyrolusMartenValidation? Validation => inner.Validation;
    public IKyrolusMartenSoftDeletePolicy? SoftDeletePolicy => inner.SoftDeletePolicy;
    public IKyrolusMartenCacheProvider? CacheProvider => cache;
    public IKyrolusMartenResiliencePolicy? ResiliencePolicy => resilience;
    public IKyrolusMartenTracing? Tracing => tracing;

    public KyrolusMartenRepositoryDecorator(
        IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey> inner,
        IKyrolusMartenCacheProvider? cache = null,
        IKyrolusMartenResiliencePolicy? resilience = null,
        IKyrolusMartenTracing? tracing = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.cache = cache;
        this.resilience = resilience;
        this.tracing = tracing;
    }

    public void SetObserver(IKyrolusMartenObserver? observer) => inner.SetObserver(observer);
    public string? ResolveTenantId(ITenantResolver? resolver) => inner.ResolveTenantId(resolver);

    private Task<T> ExecAsync<T>(string op, Func<Task<T>> action, object? target = null, CancellationToken ct = default)
    {
        return TraceAsync(op, target, () =>
        {
            return resilience is null
                ? GuardAsync(op, target, action, ct)
                : resilience.ExecuteAsync(op, () => GuardAsync(op, target, action, ct), ct);
        }, ct);
    }

    private async Task<TResult> TraceAsync<TResult>(string op, object? payload, Func<Task<TResult>> action, CancellationToken ct)
    {
        IDisposable? scope = tracing?.StartScope(op, payload);
        var sw = Stopwatch.StartNew();
        Exception? ex = null;
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception e) { ex = e; throw; }
        finally
        {
            sw.Stop();
            if (tracing is not null) await tracing.RecordAsync(op, payload, sw.Elapsed, ex, ct).ConfigureAwait(false);
            scope?.Dispose();
        }
    }

    private async Task<T> GuardAsync<T>(string op, object? target, Func<Task<T>> action, CancellationToken ct)
    {
        if (Validation is not null) await Validation.ValidateAsync(op, target, ct).ConfigureAwait(false);
        if (Authorization is not null && !await Authorization.AuthorizeAsync(op, target, ct).ConfigureAwait(false))
            throw new UnauthorizedAccessException($"Operation '{op}' not authorized.");
        return await action().ConfigureAwait(false);
    }

    public Task<(TEntity? Entity, Guid? Version)> GetByIdWithVersionAsync(TKey id, string? tenantId = null, CancellationToken cancellationToken = default)
        => ExecAsync("GetByIdWithVersion", () => inner.GetByIdWithVersionAsync(id, tenantId, cancellationToken), id, cancellationToken);

    public Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, Action<IMartenQueryable<TEntity>>? configureQuery = null, string? tenantId = null, bool includeSoftDeleted = false, CancellationToken cancellationToken = default)
        => ExecAsync("GetAll", () => inner.GetAllAsync(filter, orderBy, configureQuery, tenantId, includeSoftDeleted, cancellationToken), filter, cancellationToken);

    public Task<TEntity?> GetByIdAsync(TKey id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = $"marten:{typeof(TEntity).Name}:id:{id}";
        if (cache is null) return inner.GetByIdAsync(id, tenantId, cancellationToken);
        return TraceAsync("GetById", id, async () =>
        {
            var cached = await cache.GetAsync<TEntity>(key, cancellationToken).ConfigureAwait(false);
            if (cached is not null) return cached;
            var entity = await inner.GetByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
            if (entity is not null) await cache.SetAsync(key, entity, TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
            return entity;
        }, cancellationToken);
    }

    public Task<IEnumerable<TProjection>> QueryAsync<TProjection>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, CancellationToken cancellationToken = default) where TProjection : notnull
        => inner.QueryAsync(query, cancellationToken);

    public Task<IEnumerable<TProjection>> QueryAsync<TProjection>(IQuerySpecification<TEntity> specification, Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector, CancellationToken cancellationToken = default) where TProjection : notnull
        => inner.QueryAsync(specification, selector, cancellationToken);

    public Task<IEnumerable<TProjection>> QuerySelectAsync<TProjection>(Expression<Func<TEntity, bool>>? filter, Expression<Func<TEntity, TProjection>> selector, Action<IMartenQueryable<TEntity>>? configureQuery = null, string? tenantId = null, bool includeSoftDeleted = false, CancellationToken cancellationToken = default) where TProjection : notnull
        => inner.QuerySelectAsync(filter, selector, configureQuery, tenantId, includeSoftDeleted, cancellationToken);

    public Task<IEnumerable<TProjection>> QueryWithIncludeAsync<TProjection, TInclude>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, Action<TInclude> onInclude, CancellationToken cancellationToken = default) where TProjection : notnull
        => inner.QueryWithIncludeAsync(query, onInclude, cancellationToken);

    public Task<IReadOnlyList<TInclude>> QueryWithIncludeToListAsync<TProjection, TInclude>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, CancellationToken cancellationToken = default) where TProjection : notnull
        => inner.QueryWithIncludeToListAsync<TProjection, TInclude>(query, cancellationToken);

    public Task<IDictionary<TKeyInclude, TInclude>> QueryWithIncludeToDictionaryAsync<TProjection, TInclude, TKeyInclude>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, Func<TInclude, TKeyInclude> keySelector, CancellationToken cancellationToken = default) where TProjection : notnull where TKeyInclude : notnull
        => inner.QueryWithIncludeToDictionaryAsync<TProjection, TInclude, TKeyInclude>(query, keySelector, cancellationToken);

    public Task<PageResult<TProjection>> QueryPageAsync<TProjection>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, int pageNumber, int pageSize, CancellationToken cancellationToken = default) where TProjection : notnull
        => inner.QueryPageAsync(query, pageNumber, pageSize, cancellationToken);

    public Task<PageResult<TProjection>> QueryPageAsync<TProjection>(IQuerySpecification<TEntity> specification, Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector, int pageNumber, int pageSize, CancellationToken cancellationToken = default) where TProjection : notnull
        => inner.QueryPageAsync(specification, selector, pageNumber, pageSize, cancellationToken);

    public Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => ExecAsync("Add", () => inner.AddAsync(entity, cancellationToken), entity, cancellationToken);

    public Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        => ExecAsync("AddRange", () => inner.AddRangeAsync(entities, cancellationToken), entities, cancellationToken);

    public Task<TEntity> UpsertAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
        => ExecAsync("Upsert", () => inner.UpsertAsync(entity, expectedVersion, tenantId, cancellationToken), entity, cancellationToken);

    public Task<IEnumerable<TEntity>> UpsertRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
        => ExecAsync("UpsertRange", () => inner.UpsertRangeAsync(entities, tenantId, cancellationToken), entities, cancellationToken);

    public Task<TEntity?> UpdateAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
        => ExecAsync("Update", () => inner.UpdateAsync(entity, expectedVersion, tenantId, cancellationToken), entity, cancellationToken);

    public Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
        => ExecAsync("UpdateRange", () => inner.UpdateRangeAsync(entities, tenantId, cancellationToken), entities, cancellationToken);

    public Task<TEntity?> PatchAsync(TKey id, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default)
        => ExecAsync("Patch", () => inner.PatchAsync(id, updates, tenantId, cancellationToken), updates, cancellationToken);

    public Task<int> PatchWhereAsync(Expression<Func<TEntity, bool>> filter, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default)
        => inner.PatchWhereAsync(filter, updates, tenantId, cancellationToken);

    public Task<bool> RemoveAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
        => ExecAsync("RemoveEntity", () => inner.RemoveAsync(entity, expectedVersion, tenantId, cancellationToken), entity, cancellationToken);

    public Task<bool> RemoveAsync(TKey id, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
        => ExecAsync("RemoveById", () => inner.RemoveAsync(id, expectedVersion, tenantId, cancellationToken), id, cancellationToken);

    public Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
        => inner.DeleteWhereAsync(filter, tenantId, cancellationToken);

    public Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
        => inner.RemoveRangeAsync(entities, tenantId, cancellationToken);

    public Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
        => inner.ExistAsync(filter, tenantId, cancellationToken);

    public IAsyncEnumerable<TEntity> StreamAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, Action<IMartenQueryable<TEntity>>? configureQuery = null, string? tenantId = null, bool includeSoftDeleted = false, CancellationToken cancellationToken = default)
        => inner.StreamAsync(filter, orderBy, configureQuery, tenantId, includeSoftDeleted, cancellationToken);

    public IAsyncEnumerable<TEntity> StreamBySpecAsync(IQuerySpecification<TEntity> specification, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, Action<IMartenQueryable<TEntity>>? configureQuery = null, string? tenantId = null, bool includeSoftDeleted = false, CancellationToken cancellationToken = default)
        => inner.StreamBySpecAsync(specification, orderBy, configureQuery, tenantId, includeSoftDeleted, cancellationToken);

    public Task<TResult> ExecuteCompiledQueryAsync<TCompiled, TResult>(TCompiled query, CancellationToken cancellationToken = default) where TCompiled : ICompiledQuery<TEntity, TResult>
        => inner.ExecuteCompiledQueryAsync<TCompiled, TResult>(query, cancellationToken);

    public Task<TResult> WithSessionAsync<TResult>(MartenSessionMode mode, Func<TSession, Task<TResult>> work, CancellationToken cancellationToken = default)
        => inner.WithSessionAsync(mode, work, cancellationToken);

    public Task<int> TransformWhereAsync(Expression<Func<TEntity, bool>> filter, string transformName, object? arguments = null, string? tenantId = null, CancellationToken cancellationToken = default)
        => inner.TransformWhereAsync(filter, transformName, arguments, tenantId, cancellationToken);
}
