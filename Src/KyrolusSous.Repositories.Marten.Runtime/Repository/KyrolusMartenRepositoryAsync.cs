namespace KyrolusSous.Repositories.Marten.Runtime.Repository;

public class KyrolusMartenRepositoryAsync<TSession, TEntity, TKey>(TSession rootSession, KyrolusMartenRepositoryDependencies? services = null) : IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey>
    where TSession : IDocumentSession
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    protected TSession Session { get; } = rootSession ?? throw new ArgumentNullException(nameof(rootSession));

    public IKyrolusMartenObserver? Observer { get; private set; } = services?.Observer;
    public IKyrolusMartenAuthorization? Authorization { get; } = services?.Authorization;
    public IKyrolusMartenValidation? Validation { get; } = services?.Validation;
    public IKyrolusMartenSoftDeletePolicy? SoftDeletePolicy { get; } = services?.SoftDeletePolicy;
    public IKyrolusMartenCacheProvider? CacheProvider { get; } = services?.CacheProvider;
    public IKyrolusMartenResiliencePolicy? ResiliencePolicy { get; } = services?.ResiliencePolicy;
    public IKyrolusMartenTracing? Tracing { get; } = services?.Tracing;

    public void SetObserver(IKyrolusMartenObserver? observer) => Observer = observer;

    public string? ResolveTenantId(ITenantResolver? resolver) => resolver?.ResolveTenantId();

    private IDocumentSession ResolveSession(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return Session;
        var method = typeof(IDocumentSession).GetMethod("ForTenant", new[] { typeof(string) });
        if (method is null) return Session;
        var resolved = method.Invoke(Session, new object[] { tenantId });
        return resolved as IDocumentSession ?? Session;
    }

    private IDocumentSession ResolveSession(MartenQueryOptions<TEntity> options) => ResolveSession(options.TenantId);

    private async Task NotifyBeforeAsync(string op, object? payload, CancellationToken ct)
    {
        if (Observer is not null) await Observer.OnBeforeAsync(op, payload, ct).ConfigureAwait(false);
    }

    private async Task NotifyAfterAsync(string op, object? result, Stopwatch sw, Exception? ex, CancellationToken ct)
    {
        if (Observer is not null) await Observer.OnAfterAsync(op, result, sw.Elapsed, ex, ct).ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        await NotifyBeforeAsync("GetAll", opts.Filter, cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        Exception? ex = null;
        try
        {
            var query = BuildQuery(opts, out var session);
            var list = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            await ApplyIncludesAsync(list, opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
            return list;
        }
        catch (Exception e) { ex = e; throw; }
        finally { sw.Stop(); await NotifyAfterAsync("GetAll", null, sw, ex, cancellationToken).ConfigureAwait(false); }
    }

    public async Task<MartenEntityResult<TEntity>?> GetByIdAsync(TKey id, MartenQueryOptions<TEntity>? options = null, CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var session = ResolveSession(opts);
        var entity = await session.LoadAsync<TEntity>(id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        await ApplyIncludesAsync(entity, opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
        var metadata = await session.MetadataForAsync(entity, cancellationToken).ConfigureAwait(false);
        var version = ReadVersion(metadata);
        return new MartenEntityResult<TEntity>(entity, version);
    }

    public async Task<IEnumerable<TProjection>> QueryAsync<TProjection>(
        MartenQueryOptions<TEntity>? options,
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector,
        CancellationToken cancellationToken = default) where TProjection : notnull
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var baseQuery = BuildQuery(opts, out var session);
        var projected = selector(baseQuery);
        var list = await projected.ToListAsync(cancellationToken).ConfigureAwait(false);
        await ApplyIncludesIfEntityProjection(list, opts, session, cancellationToken).ConfigureAwait(false);
        return list;
    }

    public async Task<PageResult<TProjection>> QueryPageAsync<TProjection>(
        MartenQueryOptions<TEntity>? options,
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector,
        MartenPageRequest? page = null,
        CancellationToken cancellationToken = default) where TProjection : notnull
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var request = page ?? new MartenPageRequest();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var baseQuery = BuildQuery(opts, out var session);
        var projected = selector(baseQuery);
        var total = await projected.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await projected.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        await ApplyIncludesIfEntityProjection(items, opts, session, cancellationToken).ConfigureAwait(false);
        return new PageResult<TProjection>(items, total, pageNumber, pageSize);
    }

    public virtual async Task<PageResult<TEntity>> GetPageAsync(MartenQueryOptions<TEntity>? options = null, MartenPageRequest? page = null, CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var request = page ?? new MartenPageRequest();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var query = BuildQuery(opts, out var session);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        await ApplyIncludesAsync(items, opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
        return new PageResult<TEntity>(items, total, pageNumber, pageSize);
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
        var session = ResolveSession(tenantId);
        session.Store(entity);
        return Task.FromResult(entity);
    }

    public async Task<IEnumerable<TEntity>> UpsertRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var array = entities.ToArray();
        var session = ResolveSession(tenantId);
        session.Store(array);
        return await Task.FromResult(array);
    }

    public Task<TEntity?> UpdateAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(tenantId);
        session.Store(entity);
        return Task.FromResult<TEntity?>(entity);
    }

    public async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        // Reuse upsert pipeline to keep behavior consistent
        return await UpsertRangeAsync(entities, tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MartenEntityResult<TEntity>?> PatchAsync(TKey id, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(tenantId);
        var entity = await PatchEntityAsync(id, updates, session, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        var metadata = await session.MetadataForAsync(entity, cancellationToken).ConfigureAwait(false);
        var version = ReadVersion(metadata);
        return new MartenEntityResult<TEntity>(entity, version);
    }

    public Task<int> PatchWhereAsync(Expression<Func<TEntity, bool>> filter, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(tenantId);
        var patch = session.Patch<TEntity>(filter);
        foreach (var kv in updates) patch.Set(kv.Key, kv.Value);
        // Marten executes on SaveChanges; return 0 as placeholder
        return Task.FromResult(0);
    }

    public virtual Task<bool> RemoveAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(tenantId);
        session.Delete(entity);
        return Task.FromResult(true);
    }

    public virtual Task<bool> RemoveAsync(TKey id, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(tenantId);
        session.Delete<TEntity>(id!);
        return Task.FromResult(true);
    }

    public virtual Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(tenantId);
        session.DeleteWhere(filter);
        return Task.FromResult(0);
    }

    public Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(tenantId);
        session.Delete(entities.ToArray());
        return Task.FromResult(true);
    }

    public Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(tenantId);
        return session.Query<TEntity>().AnyAsync(filter, token: cancellationToken);
    }

    public virtual async IAsyncEnumerable<TEntity> StreamAsync(MartenQueryOptions<TEntity>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var query = BuildQuery(opts, out var session);
        await foreach (var item in query.ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            await ApplyIncludesAsync(item, opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
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
        _ = ResolveSession(tenantId);
        return Task.FromResult(0);
    }

    private IMartenQueryable<TEntity> BuildQuery(MartenQueryOptions<TEntity> opts, out IDocumentSession session)
    {
        session = ResolveSession(opts);
        IMartenQueryable<TEntity> query = opts.Specification is null
            ? session.Query<TEntity>()
            : opts.Specification.Apply(session.Query<TEntity>());

        if (opts.Filter is not null) query = (IMartenQueryable<TEntity>)query.Where(opts.Filter);
        if (opts.OrderBy is not null) query = (IMartenQueryable<TEntity>)opts.OrderBy(query);
        opts.ConfigureQuery?.Invoke(query);
        return query;
    }

    private async Task ApplyIncludesIfEntityProjection<TProjection>(IEnumerable<TProjection> items, MartenQueryOptions<TEntity> opts, IDocumentSession session, CancellationToken cancellationToken)
    {
        if (typeof(TProjection) != typeof(TEntity)) return;
        await ApplyIncludesAsync(items.Cast<TEntity>(), opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
    }

    private static Guid? ReadVersion(object? metadata)
    {
        if (metadata is null) return null;
        var type = metadata.GetType();
        var prop = type.GetProperty("Version")
            ?? type.GetProperty("ETag")
            ?? type.GetProperty("DocumentVersion")
            ?? type.GetProperty("CurrentVersion");
        if (prop is null) return null;
        var raw = prop.GetValue(metadata);
        if (raw is Guid g) return g;
        if (raw is string s && Guid.TryParse(s, out var parsed)) return parsed;
        return null;
    }

    protected async Task<TEntity?> PatchEntityAsync(TKey id, Dictionary<string, object> updates, IDocumentSession session, CancellationToken cancellationToken)
    {
        var entity = await session.LoadAsync<TEntity>(id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;

        foreach (var kv in updates)
        {
            ApplyProperty(entity, kv.Key, kv.Value);
        }
        session.Store(entity);
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

    private static List<string> MergeIncludes(List<string>? includeProperties, Expression<Func<TEntity, object?>>[]? includeExpressions)
    {
        var list = includeProperties is null ? [] : new List<string>(includeProperties);
        if (includeExpressions is null) return list;
        foreach (var expr in includeExpressions)
        {
            var name = TryGetPropertyName(expr);
            if (!string.IsNullOrWhiteSpace(name))
            {
                list.Add(name);
            }
        }
        return list;
    }

    private static string? TryGetPropertyName(Expression<Func<TEntity, object?>> expr)
    {
        var body = expr.Body is UnaryExpression u && u.NodeType == ExpressionType.Convert ? u.Operand : expr.Body;
        return body is MemberExpression m ? m.Member.Name : null;
    }

    private async Task ApplyIncludesAsync(IEnumerable<TEntity> entities, List<string>? includeProperties, Expression<Func<TEntity, object?>>[]? includeExpressions, IDocumentSession session, CancellationToken cancellationToken)
    {
        var includes = MergeIncludes(includeProperties, includeExpressions);
        if (includes.Count == 0) return;
        foreach (var entity in entities)
        {
            await ApplyIncludesAsync(entity, includes, session, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task ApplyIncludesAsync(TEntity entity, List<string>? includeProperties, Expression<Func<TEntity, object?>>[]? includeExpressions, IDocumentSession session, CancellationToken cancellationToken)
    {
        var includes = MergeIncludes(includeProperties, includeExpressions);
        if (includes.Count == 0) return Task.CompletedTask;
        return ApplyIncludesAsync(entity, includes, session, cancellationToken);
    }

    private async Task ApplyIncludesAsync(TEntity entity, List<string> includeProperties, IDocumentSession session, CancellationToken cancellationToken)
    {
        foreach (var include in includeProperties)
        {
            await ApplyIncludeAsync(entity, include, session, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ApplyIncludeAsync(TEntity entity, string includeProperty, IDocumentSession session, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(includeProperty)) return;
        var prop = typeof(TEntity).GetProperty(includeProperty, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null || !prop.CanWrite) return;
        if (prop.PropertyType == typeof(string)) return;

        if (TryGetCollectionElementType(prop.PropertyType, out var elementType))
        {
            var idsProp = ResolveIdsProperty(typeof(TEntity), prop.Name);
            if (idsProp is null) return;
            if (idsProp.GetValue(entity) is not IEnumerable idsValue) return;
            var loaded = await LoadManyAsync(elementType, idsValue, session, cancellationToken).ConfigureAwait(false);
            SetCollectionValue(entity, prop, elementType, loaded);
            return;
        }

        var idProp = ResolveIdProperty(typeof(TEntity), prop.Name);
        if (idProp is null) return;
        var idValue = idProp.GetValue(entity);
        if (idValue is null) return;
        var loadedEntity = await LoadAsync(prop.PropertyType, idValue, session, cancellationToken).ConfigureAwait(false);
        prop.SetValue(entity, loadedEntity);
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(object);
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType() ?? typeof(object);
            return true;
        }

        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            if (args.Length == 1 && typeof(IEnumerable<>).MakeGenericType(args[0]).IsAssignableFrom(type))
            {
                elementType = args[0];
                return true;
            }
        }

        var ienum = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (ienum is not null)
        {
            elementType = ienum.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private static PropertyInfo? ResolveIdProperty(Type entityType, string includeName)
    {
        var candidates = new List<string> { $"{includeName}Id" };
        if (includeName.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add($"{includeName.Substring(0, includeName.Length - 1)}Id");
        }
        foreach (var name in candidates)
        {
            var prop = entityType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is not null) return prop;
        }
        return null;
    }

    private static PropertyInfo? ResolveIdsProperty(Type entityType, string includeName)
    {
        var candidates = new List<string> { $"{includeName}Ids" };
        if (includeName.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add($"{includeName.Substring(0, includeName.Length - 1)}Ids");
        }
        foreach (var name in candidates)
        {
            var prop = entityType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is not null) return prop;
        }
        return null;
    }

    private async Task<object?> LoadAsync(Type docType, object id, IDocumentSession session, CancellationToken cancellationToken)
    {
        var method = GetLoadAsyncMethod().MakeGenericMethod(docType);
        var idParamType = method.GetParameters()[0].ParameterType;
        var typedId = ConvertId(id, idParamType);
        if (typedId is null) return null;
        var task = (Task)method.Invoke(session, [typedId, cancellationToken])!;
        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private async Task<IReadOnlyList<object>> LoadManyAsync(Type docType, IEnumerable ids, IDocumentSession session, CancellationToken cancellationToken)
    {
        var method = GetLoadManyAsyncMethod().MakeGenericMethod(docType);
        var idParamType = method.GetParameters()[0].ParameterType.GetGenericArguments()[0];
        var typedIds = CreateTypedIdList(ids, idParamType);
        var task = (Task)method.Invoke(session, [typedIds, cancellationToken])!;
        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task) is not IEnumerable result ? Array.Empty<object>() : result.Cast<object>().ToList();
    }

    private static object CreateTypedIdList(IEnumerable ids, Type idType)
    {
        var listType = typeof(List<>).MakeGenericType(idType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var raw in ids)
        {
            var converted = ConvertId(raw, idType);
            if (converted is not null) list.Add(converted);
        }
        return list;
    }

    private static object? ConvertId(object? value, Type targetType)
    {
        if (value is null) return null;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value)) return value;
        if (underlying == typeof(Guid)) return Guid.Parse(value.ToString()!);
        if (underlying.IsEnum) return Enum.Parse(underlying, value.ToString()!, true);
        return Convert.ChangeType(value, underlying);
    }

    private static void SetCollectionValue(TEntity entity, PropertyInfo prop, Type elementType, IReadOnlyList<object> items)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items.Where(item => item is not null && elementType.IsInstanceOfType(item)))
        {
            list.Add(item);
        }

        if (prop.PropertyType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            prop.SetValue(entity, array);
            return;
        }

        if (prop.PropertyType.IsAssignableFrom(listType))
        {
            prop.SetValue(entity, list);
            return;
        }

        if (prop.PropertyType.GetConstructor(Type.EmptyTypes) is not null)
        {
            var target = Activator.CreateInstance(prop.PropertyType);
            var add = prop.PropertyType.GetMethod("Add", new[] { elementType });
            if (add is not null && target is not null)
            {
                foreach (var item in list)
                {
                    add.Invoke(target, new[] { item });
                }
                prop.SetValue(entity, target);
            }
        }
    }

    private static MethodInfo GetLoadAsyncMethod() =>
        typeof(IDocumentSession).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "LoadAsync" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

    private static MethodInfo GetLoadManyAsyncMethod() =>
        typeof(IDocumentSession).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "LoadManyAsync" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
}
