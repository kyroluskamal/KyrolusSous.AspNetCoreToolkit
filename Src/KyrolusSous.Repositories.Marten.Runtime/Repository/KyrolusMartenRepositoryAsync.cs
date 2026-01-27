namespace KyrolusSous.Repositories.Marten.Runtime.Repository;

public class KyrolusMartenRepositoryAsync<TSession, TEntity, TKey>(TSession rootSession, KyrolusMartenRepositoryDependencies? services = null) : IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey>
    where TSession : IDocumentSession
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    protected TSession Session { get; } = rootSession ?? throw new ArgumentNullException(nameof(rootSession));

    public IKyrolusMartenObserver? Observer { get; private set; } = services?.Observer;
    public IKyrolusMartenAuthorization? Authorization { get; private set; } = services?.Authorization;
    public IKyrolusMartenValidation? Validation { get; private set; } = services?.Validation;
    public IKyrolusMartenSoftDeletePolicy? SoftDeletePolicy { get; private set; } = services?.SoftDeletePolicy;
    public ICacheProvider? CacheProvider { get; private set; } = services?.CacheProvider;
    private ICacheKeyContext? cacheKeyContext = services?.CacheKeyContext;
    private IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = services?.CachePolicyProvider;
    private KyrolusCachePolicy? cachePolicy = services?.CachePolicy;
    public IKyrolusMartenResiliencePolicy? ResiliencePolicy { get; private set; } = services?.ResiliencePolicy;
    public IKyrolusMartenTracing? Tracing { get; private set; } = services?.Tracing;
    private readonly IKyrolusMartenRepositoryPolicyProvider? policyProvider = services?.PolicyProvider;
    private Task? policyInitTask;
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> IdPropertyCache = new();
    public void SetObserver(IKyrolusMartenObserver? observer) => Observer = observer;
    public string? ResolveTenantId(ITenantResolver? resolver) => resolver?.ResolveTenantId();
    private string? ResolveTenantId(string? tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId)) return tenantId;
        return cacheKeyContext?.TenantId;
    }
    protected IDocumentSession ResolveSession(string? tenantId)
    {
        var resolvedTenant = ResolveTenantId(tenantId);
        if (string.IsNullOrWhiteSpace(resolvedTenant)) return Session;
        var method = typeof(IDocumentSession).GetMethod("ForTenant", new[] { typeof(string) });
        if (method is null) return Session;
        var resolved = method.Invoke(Session, [resolvedTenant]);
        return resolved as IDocumentSession ?? Session;
    }
    protected IDocumentSession ResolveSession(MartenQueryOptions<TEntity> options) => ResolveSession(options.TenantId);
    protected string BuildCacheKey(string? tenantId, TKey id, string? policySuffix = null)
    {
        var scope = ResolveCacheScope(tenantId);
        var scopePart = string.IsNullOrWhiteSpace(scope) ? string.Empty : $":scope={Uri.EscapeDataString(scope)}";
        var policyPart = string.IsNullOrWhiteSpace(policySuffix) ? string.Empty : $":policy={Uri.EscapeDataString(policySuffix)}";
        return $"{typeof(TEntity).Name}:id{scopePart}{policyPart}:{id}";
    }
    protected string BuildCacheAllKey(string? tenantId, string? policySuffix = null)
    {
        var scope = ResolveCacheScope(tenantId);
        var scopePart = string.IsNullOrWhiteSpace(scope) ? string.Empty : $":scope={Uri.EscapeDataString(scope)}";
        var policyPart = string.IsNullOrWhiteSpace(policySuffix) ? string.Empty : $":policy={Uri.EscapeDataString(policySuffix)}";
        return $"{typeof(TEntity).Name}:all{scopePart}{policyPart}";
    }
    private string BuildCompiledQueryCacheKey(object query, string? tenantId, string? policySuffix)
    {
        var scope = ResolveCacheScope(tenantId);
        var scopePart = string.IsNullOrWhiteSpace(scope) ? string.Empty : $":scope={Uri.EscapeDataString(scope)}";
        var policyPart = string.IsNullOrWhiteSpace(policySuffix) ? string.Empty : $":policy={Uri.EscapeDataString(policySuffix)}";
        var queryType = query.GetType().FullName ?? query.GetType().Name;
        var queryPart = Uri.EscapeDataString(queryType);
        var fingerprint = BuildCompiledQueryFingerprint(query);
        var fingerprintPart = string.IsNullOrWhiteSpace(fingerprint) ? string.Empty : $":args={fingerprint}";
        return $"{typeof(TEntity).Name}:compiled:{queryPart}{scopePart}{policyPart}{fingerprintPart}";
    }
    private static string BuildCompiledQueryFingerprint(object query)
    {
        var props = query.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

        var parts = new List<string>();
        foreach (var prop in props)
        {
            var name = Uri.EscapeDataString(prop.Name);
            var value = EscapeKeyPart(prop.GetValue(query));
            parts.Add($"{name}={value}");
        }

        return string.Join("|", parts);
    }
    private static string EscapeKeyPart(object? value)
    {
        if (value is null) return "null";
        if (value is string s) return Uri.EscapeDataString(s);
        if (value is IEnumerable enumerable)
        {
            var items = new List<string>();
            foreach (var item in enumerable) items.Add(EscapeKeyPart(item));
            return $"[{string.Join(",", items)}]";
        }
        if (value is IFormattable f)
        {
            var formatted = f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "null";
            return Uri.EscapeDataString(formatted);
        }
        return Uri.EscapeDataString(value.ToString() ?? "null");
    }
    private static KyrolusCachePolicy MergeCachePolicy(KyrolusCachePolicy basePolicy, KyrolusCachePolicy? overridePolicy)
    {
        if (overridePolicy is null) return basePolicy;
        var extraKeys = MergeInvalidationEntries(basePolicy.ExtraInvalidationKeys, overridePolicy.ExtraInvalidationKeys);
        var extraPatterns = MergeInvalidationEntries(basePolicy.ExtraInvalidationKeyPatterns, overridePolicy.ExtraInvalidationKeyPatterns);
        return new KyrolusCachePolicy(
            AbsoluteExpirationRelativeToNow: overridePolicy.AbsoluteExpirationRelativeToNow ?? basePolicy.AbsoluteExpirationRelativeToNow,
            SlidingExpiration: overridePolicy.SlidingExpiration ?? basePolicy.SlidingExpiration,
            Jitter: overridePolicy.Jitter ?? basePolicy.Jitter,
            NegativeCacheTtl: overridePolicy.NegativeCacheTtl ?? basePolicy.NegativeCacheTtl,
            Enabled: overridePolicy.Enabled ?? basePolicy.Enabled,
            KeySuffix: overridePolicy.KeySuffix ?? basePolicy.KeySuffix,
            ExtraInvalidationKeys: extraKeys,
            ExtraInvalidationKeyPatterns: extraPatterns);
    }
    private static IReadOnlyCollection<string>? MergeInvalidationEntries(
        IReadOnlyCollection<string>? baseEntries,
        IReadOnlyCollection<string>? overrideEntries)
    {
        if (baseEntries is null || baseEntries.Count == 0) return overrideEntries;
        if (overrideEntries is null || overrideEntries.Count == 0) return baseEntries;
        return baseEntries.Concat(overrideEntries).Distinct(StringComparer.Ordinal).ToArray();
    }
    private string? ResolveCacheScope(string? tenantId)
    {
        if (cacheKeyContext is not null && !string.IsNullOrWhiteSpace(cacheKeyContext.ScopeKey))
            return cacheKeyContext.ScopeKey;
        var resolvedTenant = ResolveTenantId(tenantId);
        if (!string.IsNullOrWhiteSpace(resolvedTenant))
            return $"tenant={resolvedTenant}";
        return null;
    }

    protected async Task EnsurePolicyInitializedAsync(CancellationToken ct)
    {
        if (policyProvider is null) return;

        var initTask = policyInitTask;
        if (initTask is null)
        {
            var newTask = InitializePolicyAsync(ct);
            initTask = Interlocked.CompareExchange(ref policyInitTask, newTask, null) ?? newTask;
        }

        await initTask.ConfigureAwait(false);
    }

    private async Task InitializePolicyAsync(CancellationToken ct)
    {
        var context = new KyrolusMartenRepositoryPolicyContext(
            typeof(TEntity),
            typeof(TEntity).Name,
            typeof(TSession),
            ResolveCacheScope(null),
            cacheKeyContext?.TenantId);

        var dynamicDeps = await policyProvider!.GetPolicyAsync(context, ct).ConfigureAwait(false);
        if (dynamicDeps is null) return;

        ApplyPolicy(dynamicDeps);
    }

    private void ApplyPolicy(KyrolusMartenRepositoryDependencies deps)
    {
        if (deps.Observer is not null) Observer = deps.Observer;
        if (deps.Authorization is not null) Authorization = deps.Authorization;
        if (deps.Validation is not null) Validation = deps.Validation;
        if (deps.SoftDeletePolicy is not null) SoftDeletePolicy = deps.SoftDeletePolicy;
        if (deps.CacheProvider is not null) CacheProvider = deps.CacheProvider;
        if (deps.CacheKeyContext is not null) cacheKeyContext = deps.CacheKeyContext;
        if (deps.CachePolicyProvider is not null) cachePolicyProvider = deps.CachePolicyProvider;
        if (deps.CachePolicy is not null) cachePolicy = deps.CachePolicy;
        if (deps.ResiliencePolicy is not null) ResiliencePolicy = deps.ResiliencePolicy;
        if (deps.Tracing is not null) Tracing = deps.Tracing;
    }
    protected async ValueTask<KyrolusCachePolicy> ResolveCachePolicyAsync(string operation, string? tenantId, CancellationToken ct)
    {
        await EnsurePolicyInitializedAsync(ct).ConfigureAwait(false);
        var effective = new KyrolusCachePolicy(
            AbsoluteExpirationRelativeToNow: DefaultCacheTtl,
            Enabled: false);
        effective = MergeCachePolicy(effective, cachePolicy);
        if (cachePolicyProvider is not null)
        {
            var context = new KyrolusRepositoryCachePolicyContext(
                typeof(TEntity),
                typeof(TEntity).Name,
                operation,
                ResolveCacheScope(tenantId),
                ResolveTenantId(tenantId));
            var dynamicPolicy = await cachePolicyProvider.GetPolicyAsync(context, ct).ConfigureAwait(false);
            effective = MergeCachePolicy(effective, dynamicPolicy);
        }
        return effective;
    }
    protected static bool IsCacheEnabled(KyrolusCachePolicy policy) => policy.Enabled.GetValueOrDefault();
    private string? ResolveCacheRegion(string? tenantId)
    {
        if (cacheKeyContext is null) return ResolveCacheScope(tenantId);
        if (!string.IsNullOrWhiteSpace(cacheKeyContext.Region)) return cacheKeyContext.Region;
        return ResolveCacheScope(tenantId);
    }
    protected KyrolusCacheEntryOptions BuildCacheEntryOptions(KyrolusCachePolicy policy, string? tenantId) => new()
    {
        AbsoluteExpirationRelativeToNow = policy.AbsoluteExpirationRelativeToNow,
        SlidingExpiration = policy.SlidingExpiration,
        Jitter = policy.Jitter,
        NegativeExpirationRelativeToNow = policy.NegativeCacheTtl,
        Region = ResolveCacheRegion(tenantId),
        TenantId = ResolveTenantId(tenantId)
    };
    private IEnumerable<string> ExpandInvalidationTemplates(
        IReadOnlyCollection<string>? templates,
        TKey? id,
        bool hasId,
        string? tenantId,
        KyrolusCachePolicy policy)
    {
        if (templates is null || templates.Count == 0) yield break;

        var scope = ResolveCacheScope(tenantId);
        var resolvedTenant = ResolveTenantId(tenantId);
        var allKey = BuildCacheAllKey(tenantId, policy.KeySuffix);
        var idKey = hasId ? BuildCacheKey(tenantId, id!, policy.KeySuffix) : null;
        foreach (var template in templates)
        {
            if (string.IsNullOrWhiteSpace(template)) continue;
            if (!hasId && template.Contains("{id}", StringComparison.Ordinal)) continue;

            var expanded = template
                .Replace("{entity}", typeof(TEntity).Name, StringComparison.Ordinal)
                .Replace("{tenant}", resolvedTenant ?? string.Empty, StringComparison.Ordinal)
                .Replace("{scope}", scope ?? string.Empty, StringComparison.Ordinal)
                .Replace("{policy}", policy.KeySuffix ?? string.Empty, StringComparison.Ordinal)
                .Replace("{all}", allKey, StringComparison.Ordinal);
            expanded = expanded.Replace("{id}", idKey ?? string.Empty, StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(expanded)) yield return expanded;
        }
    }
    private async Task RemoveExtraInvalidationKeysAsync(
        TKey? id,
        bool hasId,
        string? tenantId,
        KyrolusCachePolicy policy,
        CancellationToken cancellationToken)
    {
        if (CacheProvider is null) return;
        foreach (var key in ExpandInvalidationTemplates(policy.ExtraInvalidationKeys, id, hasId, tenantId, policy))

            await CacheProvider.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        foreach (var pattern in ExpandInvalidationTemplates(policy.ExtraInvalidationKeyPatterns, id, hasId, tenantId, policy))
            await CacheProvider.RemoveKeysByPatternAsync(pattern, cancellationToken).ConfigureAwait(false);
    }
    protected virtual IEnumerable<string> GetAdditionalEntityCacheKeysForInvalidation(
        TKey id,
        string? tenantId,
        KyrolusCachePolicy policy)
        => [];
    protected virtual IEnumerable<string> GetAdditionalAllCacheKeysForInvalidation(
        string? tenantId,
        KyrolusCachePolicy policy)
        => [];
    private static bool TryGetEntityId(TEntity entity, out TKey id)
    {
        id = default!;
        if (entity is null) return false;

        var prop = IdPropertyCache.GetOrAdd(typeof(TEntity), type =>
            type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? type.GetProperty($"{type.Name}Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));

        if (prop is null) return false;

        var value = prop.GetValue(entity);
        if (value is null) return false;

        if (value is TKey typed)
        {
            id = typed;
            return true;
        }
        try
        {
            id = (TKey)Convert.ChangeType(value, typeof(TKey), System.Globalization.CultureInfo.InvariantCulture)!;
            return true;
        }
        catch
        {
            return false;
        }
    }
    private async Task InvalidateCacheAsync(TKey id, string? tenantId, CancellationToken cancellationToken)
    {
        if (CacheProvider is null) return;
        var resolvedPolicy = await ResolveCachePolicyAsync("InvalidateCacheAsync", tenantId, cancellationToken).ConfigureAwait(false);
        if (!IsCacheEnabled(resolvedPolicy)) return;

        var key = BuildCacheKey(tenantId, id, resolvedPolicy.KeySuffix);
        await CacheProvider.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        foreach (var extra in GetAdditionalEntityCacheKeysForInvalidation(id, tenantId, resolvedPolicy))
            await CacheProvider.RemoveAsync(extra, cancellationToken).ConfigureAwait(false);
        await RemoveExtraInvalidationKeysAsync(id, true, tenantId, resolvedPolicy, cancellationToken).ConfigureAwait(false);
        var allKey = BuildCacheAllKey(tenantId, resolvedPolicy.KeySuffix);
        await CacheProvider.RemoveAsync(allKey, cancellationToken).ConfigureAwait(false);
        foreach (var extra in GetAdditionalAllCacheKeysForInvalidation(tenantId, resolvedPolicy))
            await CacheProvider.RemoveAsync(extra, cancellationToken).ConfigureAwait(false);
        await RemoveExtraInvalidationKeysAsync(default, false, tenantId, resolvedPolicy, cancellationToken).ConfigureAwait(false);
    }
    private Task InvalidateCacheAsync(TEntity entity, string? tenantId, CancellationToken cancellationToken)
    {
        if (!TryGetEntityId(entity, out var id)) return Task.CompletedTask;
        return InvalidateCacheAsync(id, tenantId, cancellationToken);
    }
    private async Task InvalidateCacheAsync(IEnumerable<TEntity> entities, string? tenantId, CancellationToken cancellationToken)
    {
        if (CacheProvider is null) return;
        var resolvedPolicy = await ResolveCachePolicyAsync("InvalidateCacheAsync", tenantId, cancellationToken).ConfigureAwait(false);
        if (!IsCacheEnabled(resolvedPolicy)) return;

        var tasks = new List<Task>();
        var allKey = BuildCacheAllKey(tenantId, resolvedPolicy.KeySuffix);
        tasks.Add(CacheProvider.RemoveAsync(allKey, cancellationToken));
        foreach (var extra in GetAdditionalAllCacheKeysForInvalidation(tenantId, resolvedPolicy))
            tasks.Add(CacheProvider.RemoveAsync(extra, cancellationToken));
        tasks.Add(RemoveExtraInvalidationKeysAsync(default, false, tenantId, resolvedPolicy, cancellationToken));
        foreach (var entity in entities)
        {
            if (!TryGetEntityId(entity, out var id)) continue;
            var key = BuildCacheKey(tenantId, id, resolvedPolicy.KeySuffix);
            tasks.Add(CacheProvider.RemoveAsync(key, cancellationToken));
            foreach (var extra in GetAdditionalEntityCacheKeysForInvalidation(id, tenantId, resolvedPolicy))
                tasks.Add(CacheProvider.RemoveAsync(extra, cancellationToken));
            tasks.Add(RemoveExtraInvalidationKeysAsync(id, true, tenantId, resolvedPolicy, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
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
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var hasIncludes = (opts.IncludeProperties?.Count ?? 0) > 0 || (opts.IncludeExpressions?.Length ?? 0) > 0;
        var isCacheable = !hasIncludes
            && opts.Filter is null
            && opts.Specification is null
            && opts.OrderBy is null
            && opts.ConfigureQuery is null
            && !opts.IncludeSoftDeleted;
        await NotifyBeforeAsync("GetAll", opts.Filter, cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        Exception? ex = null;
        try
        {
            if (CacheProvider is not null && isCacheable)
            {
                var resolvedPolicy = await ResolveCachePolicyAsync("GetAllAsync", opts.TenantId, cancellationToken).ConfigureAwait(false);
                if (IsCacheEnabled(resolvedPolicy))
                {
                    var cacheKey = BuildCacheAllKey(opts.TenantId, resolvedPolicy.KeySuffix);
                    var optionsEntry = BuildCacheEntryOptions(resolvedPolicy, opts.TenantId);
                    return await CacheProvider.GetOrCreateAsync<List<TEntity>>(cacheKey,
                        async ct =>
                        {
                            var query = BuildQuery(opts, out var session);
                            var list = await query.ToListAsync(ct).ConfigureAwait(false);
                            var materialized = list is List<TEntity> typed ? typed : list.ToList();
                            await ApplyIncludesAsync(materialized, opts.IncludeProperties, opts.IncludeExpressions, session, ct).ConfigureAwait(false);
                            return materialized;
                        },
                        optionsEntry,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            var query = BuildQuery(opts, out var session);
            var list = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            await ApplyIncludesAsync(list, opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
            return list;
        }
        catch (Exception e) { ex = e; throw; }
        finally { sw.Stop(); await NotifyAfterAsync("GetAll", null, sw, ex, cancellationToken).ConfigureAwait(false); }
    }
    public virtual async Task<MartenEntityResult<TEntity>?> GetByIdAsync(TKey id, MartenQueryOptions<TEntity>? options = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var session = ResolveSession(opts);
        var hasIncludes = (opts.IncludeProperties?.Count ?? 0) > 0 || (opts.IncludeExpressions?.Length ?? 0) > 0;

        if (CacheProvider is not null && !hasIncludes)
        {
            var resolvedPolicy = await ResolveCachePolicyAsync("GetByIdAsync", opts.TenantId, cancellationToken).ConfigureAwait(false);
            if (IsCacheEnabled(resolvedPolicy))
            {
                var cacheKey = BuildCacheKey(opts.TenantId, id, resolvedPolicy.KeySuffix);
                var cacheOptions = BuildCacheEntryOptions(resolvedPolicy, opts.TenantId);
                return await CacheProvider.GetOrCreateAsync<MartenEntityResult<TEntity>?>(cacheKey,
                    async ct =>
                    {
                        var cachedEntity = await session.LoadAsync<TEntity>(id, ct).ConfigureAwait(false);
                        if (cachedEntity is null) return null;
                        await ApplyIncludesAsync(cachedEntity, opts.IncludeProperties, opts.IncludeExpressions, session, ct).ConfigureAwait(false);
                        var cachedMetadata = await session.MetadataForAsync(cachedEntity, ct).ConfigureAwait(false);
                        var cachedVersion = ReadVersion(cachedMetadata);
                        return new MartenEntityResult<TEntity>(cachedEntity, cachedVersion);
                    },
                    cacheOptions,
                    cancellationToken).ConfigureAwait(false);
            }
        }
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
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
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
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
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
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
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
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        Session.Store(entity);
        await InvalidateCacheAsync(entity, null, cancellationToken).ConfigureAwait(false);
        return entity;
    }
    public async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var array = entities.ToArray();
        Session.Store(array);
        await InvalidateCacheAsync(array, null, cancellationToken).ConfigureAwait(false);
        return array;
    }
    public async Task<TEntity> UpsertAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        session.Store(entity);
        await InvalidateCacheAsync(entity, tenantId, cancellationToken).ConfigureAwait(false);
        return entity;
    }
    public async Task<IEnumerable<TEntity>> UpsertRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var array = entities.ToArray();
        var session = ResolveSession(tenantId);
        session.Store(array);
        await InvalidateCacheAsync(array, tenantId, cancellationToken).ConfigureAwait(false);
        return array;
    }
    public async Task<TEntity?> UpdateAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        if (expectedVersion.HasValue) session.UpdateExpectedVersion(entity, expectedVersion.Value);
        else session.Store(entity);
        await InvalidateCacheAsync(entity, tenantId, cancellationToken).ConfigureAwait(false);
        return entity;
    }
    public async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await UpsertRangeAsync(entities, tenantId, cancellationToken).ConfigureAwait(false);
    }
    public async Task<MartenEntityResult<TEntity>?> PatchAsync(TKey id, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        var entity = await PatchEntityAsync(id, updates, session, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        var metadata = await session.MetadataForAsync(entity, cancellationToken).ConfigureAwait(false);
        var version = ReadVersion(metadata);
        await InvalidateCacheAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return new MartenEntityResult<TEntity>(entity, version);
    }
    public async Task<int> PatchWhereAsync(Expression<Func<TEntity, bool>> filter, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        var patch = session.Patch<TEntity>(filter);
        foreach (var kv in updates) patch.Set(kv.Key, kv.Value);
        // Marten executes on SaveChanges; return 0 as placeholder
        return 0;
    }
    public virtual async Task<bool> RemoveAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        session.Delete(entity);
        await InvalidateCacheAsync(entity, tenantId, cancellationToken).ConfigureAwait(false);
        return true;
    }
    public virtual async Task<bool> RemoveAsync(TKey id, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        session.Delete<TEntity>(id!);
        await InvalidateCacheAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return true;
    }
    public virtual async Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        session.DeleteWhere(filter);
        return 0;
    }
    public async Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        var array = entities.ToArray();
        session.Delete(array);
        await InvalidateCacheAsync(array, tenantId, cancellationToken).ConfigureAwait(false);
        return true;
    }
    public async Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var session = ResolveSession(tenantId);
        return await session.Query<TEntity>().AnyAsync(filter, token: cancellationToken).ConfigureAwait(false);
    }
    public virtual async IAsyncEnumerable<TEntity> StreamAsync(MartenQueryOptions<TEntity>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var query = BuildQuery(opts, out var session);
        await foreach (var item in query.ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            await ApplyIncludesAsync(item, opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
            yield return item;
        }
    }
    public async Task<TResult> ExecuteCompiledQueryAsync<TCompiled, TResult>(TCompiled query, CancellationToken cancellationToken = default)
        where TCompiled : ICompiledQuery<TEntity, TResult>
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (CacheProvider is null)
            return await Session.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        var tenantId = ResolveTenantId((string?)null);
        var resolvedPolicy = await ResolveCachePolicyAsync("ExecuteCompiledQueryAsync", tenantId, cancellationToken).ConfigureAwait(false);
        if (!IsCacheEnabled(resolvedPolicy))
            return await Session.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        var cacheKey = BuildCompiledQueryCacheKey(query!, tenantId, resolvedPolicy.KeySuffix);
        var cacheOptions = BuildCacheEntryOptions(resolvedPolicy, tenantId);
        return await CacheProvider.GetOrCreateAsync<TResult>(
            cacheKey,
            ct => Session.QueryAsync(query, ct),
            cacheOptions,
            cancellationToken).ConfigureAwait(false);
    }
    public async Task<TResult> WithSessionAsync<TResult>(MartenSessionMode mode, Func<TSession, Task<TResult>> work, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await work(Session).ConfigureAwait(false);
    }
    public async Task<int> TransformWhereAsync(Expression<Func<TEntity, bool>> filter, string transformName, object? arguments = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        _ = ResolveSession(tenantId);
        return 0;
    }
    protected IMartenQueryable<TEntity> BuildQuery(MartenQueryOptions<TEntity> opts, out IDocumentSession session)
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
    protected static Guid? ReadVersion(object? metadata)
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

        foreach (var kv in updates) ApplyProperty(entity, kv.Key, kv.Value);
        session.Store(entity);
        return entity;
    }
    protected static void ApplyProperty(TEntity entity, string propertyName, object? rawValue)
    {
        var prop = typeof(TEntity).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || !prop.CanWrite) return;

        var value = NormalizeValue(rawValue, prop.PropertyType);
        if (value != null || prop.PropertyType.IsClass) prop.SetValue(entity, value);
    }
    protected static object? NormalizeValue(object? rawValue, Type targetType)
    {
        if (rawValue is JsonElement je)
            rawValue = je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
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
            if (!string.IsNullOrWhiteSpace(name)) list.Add(name);
        }
        return list;
    }
    private static string? TryGetPropertyName(Expression<Func<TEntity, object?>> expr)
    {
        var body = expr.Body is UnaryExpression u && u.NodeType == ExpressionType.Convert ? u.Operand : expr.Body;
        return body is MemberExpression m ? m.Member.Name : null;
    }
    protected async Task ApplyIncludesAsync(IEnumerable<TEntity> entities, List<string>? includeProperties, Expression<Func<TEntity, object?>>[]? includeExpressions, IDocumentSession session, CancellationToken cancellationToken)
    {
        var includes = MergeIncludes(includeProperties, includeExpressions);
        if (includes.Count == 0) return;
        foreach (var entity in entities)
            await ApplyIncludesAsync(entity, includes, session, cancellationToken).ConfigureAwait(false);
    }
    protected Task ApplyIncludesAsync(TEntity entity, List<string>? includeProperties, Expression<Func<TEntity, object?>>[]? includeExpressions, IDocumentSession session, CancellationToken cancellationToken)
    {
        var includes = MergeIncludes(includeProperties, includeExpressions);
        if (includes.Count == 0) return Task.CompletedTask;
        return ApplyIncludesAsync(entity, includes, session, cancellationToken);
    }
    protected async Task ApplyIncludesAsync(TEntity entity, List<string> includeProperties, IDocumentSession session, CancellationToken cancellationToken)
    {
        foreach (var include in includeProperties)
            await ApplyIncludeAsync(entity, include, session, cancellationToken).ConfigureAwait(false);
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
            candidates.Add($"{includeName.Substring(0, includeName.Length - 1)}Id");
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
            candidates.Add($"{includeName.Substring(0, includeName.Length - 1)}Ids");
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
            list.Add(item);
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
                    add.Invoke(target, [item]);
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
