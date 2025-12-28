namespace KyrolusSous.Repositories.Marten.Runtime.Repository;

public class KyrolusMartenRepositoryAsync<TSession, TEntity, TKey> : IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey>
    where TSession : IDocumentSession
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    protected TSession Session { get; }

    public IKyrolusMartenObserver? Observer { get; private set; }
    public IKyrolusMartenAuthorization? Authorization { get; }
    public IKyrolusMartenValidation? Validation { get; }
    public IKyrolusMartenSoftDeletePolicy? SoftDeletePolicy { get; }
    public IKyrolusMartenCacheProvider? CacheProvider { get; }
    public IKyrolusMartenResiliencePolicy? ResiliencePolicy { get; }
    public IKyrolusMartenTracing? Tracing { get; }

    public KyrolusMartenRepositoryAsync(TSession session, KyrolusMartenRepositoryDependencies? services = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Observer = services?.Observer;
        Authorization = services?.Authorization;
        Validation = services?.Validation;
        SoftDeletePolicy = services?.SoftDeletePolicy;
        CacheProvider = services?.CacheProvider;
        ResiliencePolicy = services?.ResiliencePolicy;
        Tracing = services?.Tracing;
    }

    public void SetObserver(IKyrolusMartenObserver? observer) => Observer = observer;

    public string? ResolveTenantId(ITenantResolver? resolver) => resolver?.ResolveTenantId();

    private async Task NotifyBeforeAsync(string op, object? payload, CancellationToken ct)
    {
        if (Observer is not null) await Observer.OnBeforeAsync(op, payload, ct).ConfigureAwait(false);
    }

    private async Task NotifyAfterAsync(string op, object? result, Stopwatch sw, Exception? ex, CancellationToken ct)
    {
        if (Observer is not null) await Observer.OnAfterAsync(op, result, sw.Elapsed, ex, ct).ConfigureAwait(false);
    }

    public async Task<(TEntity? Entity, Guid? Version)> GetByIdWithVersionAsync(TKey id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await NotifyBeforeAsync("GetByIdWithVersion", id, cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        Exception? ex = null;
        try
        {
            var entity = await Session.LoadAsync<TEntity>(id, cancellationToken).ConfigureAwait(false);
            return (entity, null);
        }
        catch (Exception e) { ex = e; throw; }
        finally { sw.Stop(); await NotifyAfterAsync("GetByIdWithVersion", null, sw, ex, cancellationToken).ConfigureAwait(false); }
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Action<IMartenQueryable<TEntity>>? configureQuery = null,
        string? tenantId = null,
        bool includeSoftDeleted = false,
        CancellationToken cancellationToken = default)
    {
        await NotifyBeforeAsync("GetAll", filter, cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        Exception? ex = null;
        try
        {
            IMartenQueryable<TEntity> query = Session.Query<TEntity>();
            if (filter is not null) query = (IMartenQueryable<TEntity>)query.Where(filter);
            if (orderBy is not null) query = (IMartenQueryable<TEntity>)orderBy(query);
            configureQuery?.Invoke(query);
            var list = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            return list;
        }
        catch (Exception e) { ex = e; throw; }
        finally { sw.Stop(); await NotifyAfterAsync("GetAll", null, sw, ex, cancellationToken).ConfigureAwait(false); }
    }

    public Task<TEntity?> GetByIdAsync(TKey id, string? tenantId = null, CancellationToken cancellationToken = default)
        => Session.LoadAsync<TEntity>(id, cancellationToken);

    public async Task<IEnumerable<TProjection>> QueryAsync<TProjection>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, CancellationToken cancellationToken = default) where TProjection : notnull
    {
        IMartenQueryable<TEntity> baseQuery = Session.Query<TEntity>();
        var projected = query(baseQuery);
        return await projected.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<TProjection>> QueryAsync<TProjection>(IQuerySpecification<TEntity> specification, Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector, CancellationToken cancellationToken = default) where TProjection : notnull
    {
        IMartenQueryable<TEntity> baseQuery = specification.Apply(Session.Query<TEntity>());
        var projected = selector(baseQuery);
        return await projected.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<TProjection>> QuerySelectAsync<TProjection>(Expression<Func<TEntity, bool>>? filter, Expression<Func<TEntity, TProjection>> selector, Action<IMartenQueryable<TEntity>>? configureQuery = null, string? tenantId = null, bool includeSoftDeleted = false, CancellationToken cancellationToken = default) where TProjection : notnull
    {
        IMartenQueryable<TEntity> q = Session.Query<TEntity>();
        if (filter is not null) q = (IMartenQueryable<TEntity>)q.Where(filter);
        configureQuery?.Invoke(q);
        return await q.Select(selector).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IEnumerable<TProjection>> QueryWithIncludeAsync<TProjection, TInclude>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, Action<TInclude> onInclude, CancellationToken cancellationToken = default) where TProjection : notnull
    {
        // Marten Include requires target collections; keep simple by executing selector then callback.
        return ExecuteWithIncludeAsync(query, onInclude, cancellationToken);
    }

    private async Task<IEnumerable<TProjection>> ExecuteWithIncludeAsync<TProjection, TInclude>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, Action<TInclude> onInclude, CancellationToken cancellationToken) where TProjection : notnull
    {
        _ = onInclude; // include hook not implemented in baseline runtime
        var results = await query(Session.Query<TEntity>()).ToListAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }

    public Task<IReadOnlyList<TInclude>> QueryWithIncludeToListAsync<TProjection, TInclude>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, CancellationToken cancellationToken = default) where TProjection : notnull
        => Task.FromResult((IReadOnlyList<TInclude>)Array.Empty<TInclude>());

    public Task<IDictionary<TKeyInclude, TInclude>> QueryWithIncludeToDictionaryAsync<TProjection, TInclude, TKeyInclude>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, Func<TInclude, TKeyInclude> keySelector, CancellationToken cancellationToken = default) where TProjection : notnull where TKeyInclude : notnull
        => Task.FromResult((IDictionary<TKeyInclude, TInclude>)new Dictionary<TKeyInclude, TInclude>());

    public async Task<PageResult<TProjection>> QueryPageAsync<TProjection>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query, int pageNumber, int pageSize, CancellationToken cancellationToken = default) where TProjection : notnull
    {
        var baseQuery = Session.Query<TEntity>();
        var projected = query(baseQuery);
        var total = await projected.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await projected.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<TProjection>(items, total, pageNumber, pageSize);
    }

    public async Task<PageResult<TProjection>> QueryPageAsync<TProjection>(IQuerySpecification<TEntity> specification, Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector, int pageNumber, int pageSize, CancellationToken cancellationToken = default) where TProjection : notnull
    {
        IMartenQueryable<TEntity> baseQuery = specification.Apply(Session.Query<TEntity>());
        var projected = selector(baseQuery);
        var total = await projected.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await projected.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<TProjection>(items, total, pageNumber, pageSize);
    }

    public Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Session.Store(entity);
        return Task.FromResult(entity);
    }

    public Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        Session.Store(entities.ToArray());
        return Task.FromResult(entities);
    }

    public Task<TEntity> UpsertAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        Session.Store(entity);
        return Task.FromResult(entity);
    }

    public async Task<IEnumerable<TEntity>> UpsertRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var array = entities.ToArray();
        Session.Store(array);
        return await Task.FromResult(array);
    }

    public Task<TEntity?> UpdateAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        Session.Store(entity);
        return Task.FromResult<TEntity?>(entity);
    }

    public async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        // Reuse upsert pipeline to keep behavior consistent
        return await UpsertRangeAsync(entities, tenantId, cancellationToken).ConfigureAwait(false);
    }

    public Task<TEntity?> PatchAsync(TKey id, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return PatchEntityAsync(id, updates, cancellationToken);
    }

    public Task<int> PatchWhereAsync(Expression<Func<TEntity, bool>> filter, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var patch = Session.Patch<TEntity>(filter);
        foreach (var kv in updates) patch.Set(kv.Key, kv.Value);
        // Marten executes on SaveChanges; return 0 as placeholder
        return Task.FromResult(0);
    }

    public virtual Task<bool> RemoveAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        Session.Delete(entity);
        return Task.FromResult(true);
    }

    public virtual Task<bool> RemoveAsync(TKey id, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        Session.Delete<TEntity>(id!);
        return Task.FromResult(true);
    }

    public virtual Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        Session.DeleteWhere(filter);
        return Task.FromResult(0);
    }

    public Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        Session.Delete(entities.ToArray());
        return Task.FromResult(true);
    }

    public Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
        => Session.Query<TEntity>().AnyAsync(filter, token: cancellationToken);

    public virtual async IAsyncEnumerable<TEntity> StreamAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, Action<IMartenQueryable<TEntity>>? configureQuery = null, string? tenantId = null, bool includeSoftDeleted = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IMartenQueryable<TEntity> query = Session.Query<TEntity>();
        if (filter is not null) query = (IMartenQueryable<TEntity>)query.Where(filter);
        if (orderBy is not null) query = (IMartenQueryable<TEntity>)orderBy(query);
        configureQuery?.Invoke(query);
        await foreach (var item in query.ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    public virtual async IAsyncEnumerable<TEntity> StreamBySpecAsync(IQuerySpecification<TEntity> specification, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, Action<IMartenQueryable<TEntity>>? configureQuery = null, string? tenantId = null, bool includeSoftDeleted = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IMartenQueryable<TEntity> query = specification.Apply(Session.Query<TEntity>());
        if (orderBy is not null) query = (IMartenQueryable<TEntity>)orderBy(query);
        configureQuery?.Invoke(query);
        await foreach (var item in query.ToAsyncEnumerable(token: cancellationToken).WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    public Task<TResult> ExecuteCompiledQueryAsync<TCompiled, TResult>(TCompiled query, CancellationToken cancellationToken = default) where TCompiled : ICompiledQuery<TEntity, TResult>
        => Session.QueryAsync(query, cancellationToken);

    public async Task<TResult> WithSessionAsync<TResult>(MartenSessionMode mode, Func<TSession, Task<TResult>> work, CancellationToken cancellationToken = default)
    {
        return await work(Session).ConfigureAwait(false);
    }

    public Task<int> TransformWhereAsync(Expression<Func<TEntity, bool>> filter, string transformName, object? arguments = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    protected async Task<TEntity?> PatchEntityAsync(TKey id, Dictionary<string, object> updates, CancellationToken cancellationToken)
    {
        var entity = await Session.LoadAsync<TEntity>(id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;

        foreach (var kv in updates)
        {
            ApplyProperty(entity, kv.Key, kv.Value);
        }
        Session.Store(entity);
        return entity;
    }

    protected static void ApplyProperty(TEntity entity, string propertyName, object? rawValue)
    {
        var prop = typeof(TEntity).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || !prop.CanWrite) return;

        var value = NormalizeValue(rawValue, prop.PropertyType);
        if (value != null || prop.PropertyType.IsClass)
        {
            prop.SetValue(entity, value);
        }
    }

    protected static object? NormalizeValue(object? rawValue, Type targetType)
    {
        if (rawValue is JsonElement je)
        {
            rawValue = je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        if (rawValue is null) return null;
        if (targetType.IsInstanceOfType(rawValue)) return rawValue;
        return Convert.ChangeType(rawValue, targetType);
    }
}
