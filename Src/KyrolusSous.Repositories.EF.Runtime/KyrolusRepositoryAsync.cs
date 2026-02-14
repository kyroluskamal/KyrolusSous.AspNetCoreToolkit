namespace KyrolusSous.Repositories.EF.Runtime;
/// <summary>
/// Generic repository implementation that mirrors the generated repository features:
/// observer hooks, optional caching, optional global filters, bulk fallbacks, compiled queries,
/// paging with specifications, and full cancellation token flow.
/// </summary>
public class KyrolusRepositoryAsync<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TKey> :
    IKyrolusRepositoryAsync<TDbContext, TEntity, TKey>
    where TDbContext : DbContext
    where TEntity : class
{
    #region Fields and ctor
    protected readonly TDbContext db;
    protected readonly DbSet<TEntity> set;
    protected KyrolusRepositoryPolicy policy = KyrolusRepositoryPolicy.Default;
    protected readonly IKyrolusRepositoryObserver? observer;
    protected readonly IKyrolusBulkExecutor<TEntity>? bulkExecutor;
    protected readonly ICacheProvider? cache;
    protected IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider;
    protected readonly bool cachePolicyProviderOverride;
    protected readonly bool enableCaching;
    protected readonly TimeSpan? cacheTtl;
    protected readonly string cacheAllKey;
    protected readonly ICacheKeyContext? cacheKeyContext;
    protected readonly string? cacheAllKeyBase;
    protected Func<IQueryable<TEntity>, IQueryable<TEntity>>? globalQueryFilter;
    protected bool softDeleteEnabled;
    protected string softDeleteProperty;
    protected string? rowVersionProperty;
    protected bool splitQueryDefault;
    protected bool asNoTrackingDefault;
    protected readonly string[] keyPropertyNames;
    protected string[] policyDefaultIncludeProperties;
    protected KyrolusDefaultIncludeMode defaultIncludeMode;
    protected readonly IKyrolusRepositoryPolicyProvider? policyProvider;
    private Task? policyInitTask;
    protected static readonly ConcurrentDictionary<(Type Type, bool SoftDelete, string SoftDeleteProperty, string DefaultIncludesKey, bool AsNoTracking, bool UseSplitQuery, string KeyName), Func<TDbContext, TKey, IAsyncEnumerable<TEntity>>> CompiledById = new();
    private static readonly ConcurrentDictionary<(Type Type, bool SoftDelete, string SoftDeleteProperty, string DefaultIncludesKey, string FilterFingerprint, bool AsNoTracking, bool UseSplitQuery), Func<TDbContext, IAsyncEnumerable<TEntity>>> CompiledGetAllFiltered = new();
    protected sealed record MaterializeByIdCommand
    (
        object?[]? KeyValues,
        bool IncludeDeleted,
        List<string>? IncludeProperties,
        IncludeGraph<TEntity>? IncludeGraph,
        Expression<Func<TEntity, object?>>[] IncludeExpressions,
        bool? AsNoTracking,
        bool? UseSplitQuery,
        CancellationToken CancellationToken
    );
    protected record GetAllCommand(
            string OperationName,
            bool CachePredicate,
            Expression<Func<TEntity, bool>>? Filter,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy,
            List<string>? IncludeProperties,
            IncludeGraph<TEntity>? IncludeGraph,
            bool? AsNoTracking,
            bool? UseSplitQuery,
            CancellationToken CancellationToken, bool DeletedOnly = false, bool IncludeDeleted = false,
            params Expression<Func<TEntity, object?>>[]? IncludeExpressions);
    protected record GetByIdCommand(
        string OperationName,
        bool CachePredicate,
        object?[]? KeyValues, List<string>? IncludeProperties,
                IncludeGraph<TEntity>? IncludeGraph, bool? AsNoTracking = null,
        bool? UseSplitQuery = null, bool IncludeDeleted = false,
        CancellationToken CancellationToken = default,
        params Expression<Func<TEntity, object?>>[] IncludeExpressions
    );
#pragma warning disable S107
    public KyrolusRepositoryAsync(
        TDbContext db,
        KyrolusRepositoryPolicy? policy = null,
        IKyrolusRepositoryObserver? observer = null,
        IKyrolusBulkExecutor<TEntity>? bulkExecutor = null,
        ICacheProvider? cache = null,
        bool enableCaching = false,
        int? cacheTtlSeconds = null,
        ICacheKeyContext? cacheKeyContext = null,
        IKyrolusRepositoryCachePolicyProvider? cachePolicyProvider = null,
        IKyrolusRepositoryPolicyProvider? policyProvider = null
)
    {
        this.db = db ?? throw new ArgumentNullException(nameof(db));
        set = db.Set<TEntity>();
        this.policy = policy ?? KyrolusRepositoryPolicy.Default;
        this.observer = observer;
        this.bulkExecutor = bulkExecutor;
        this.cache = cache;
        this.cachePolicyProviderOverride = cachePolicyProvider is not null;
        this.cachePolicyProvider = cachePolicyProvider ?? this.policy.CachePolicyProvider;
        this.enableCaching = enableCaching;
        cacheTtl = cacheTtlSeconds is > 0 ? TimeSpan.FromSeconds(cacheTtlSeconds.Value) : null;
        cacheAllKey = $"{typeof(TEntity).Name}:all";
        globalQueryFilter = this.policy?.GetGlobalQueryFilter<TEntity>();
        softDeleteEnabled = false;
        softDeleteProperty = string.IsNullOrWhiteSpace(this.policy?.SoftDeleteProperty)
            ? "IsDeleted"
            : this.policy.SoftDeleteProperty!;
        rowVersionProperty = this.policy?.RowVersionProperty;
        splitQueryDefault = this.policy?.UseSplitQueryDefault ?? false;
        asNoTrackingDefault = this.policy?.AsNoTrackingDefault ?? true;
        keyPropertyNames = GetPrimaryKeyNames();
        this.cacheKeyContext = cacheKeyContext;
        cacheAllKeyBase = $"{typeof(TEntity).Name}:all";
        defaultIncludeMode = this.policy?.DefaultIncludeMode ?? KyrolusDefaultIncludeMode.Merge;
        policyDefaultIncludeProperties = this.policy?.GetDefaultIncludeProperties<TEntity>() ?? [];
        this.policyProvider = this.policy?.PolicyProvider ?? policyProvider;
    }
#pragma warning restore S107
    #endregion


    #region GetAll
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    => await GetAllInternalAsync(new GetAllCommand(nameof(GetAllAsync), (includeProperties is not { Count: > 0 })
                    && (includeGraph is not { Includes.Count: > 0 }), filter, orderBy, includeProperties, includeGraph, asNoTracking, useSplitQuery, cancellationToken)).ConfigureAwait(false);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
        bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    => await GetAllInternalAsync(new GetAllCommand(nameof(GetAllAsync), includeExpressions is not { Length: > 0 }, filter, orderBy, null, null,
    asNoTracking, useSplitQuery, cancellationToken, false, false, includeExpressions)).ConfigureAwait(false);
    #endregion

    #region Compiled queries
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<List<TEntity>> GetAllCompiledAsync(Expression<Func<TEntity, bool>> filter,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default)
    {
        if (filter is null
            || (filter.Body is ConstantExpression c && c.Value is bool b && b))
            throw new ArgumentException("Compiled queries require a non-trivial filter. Use GetAllAsync for unfiltered queries.", nameof(filter));
        var requestedNoTracking = asNoTracking ?? asNoTrackingDefault;
        var requestedSplit = useSplitQuery ?? splitQueryDefault;
        var useSoftDelete = softDeleteEnabled && !string.IsNullOrWhiteSpace(softDeleteProperty);
        var defaultIncludeProperties = policyDefaultIncludeProperties;
        var defaultIncludesKey = GetDefaultIncludesKey();
        var filterFingerprint = KyrolusExpressionFingerprint.Build(filter);
        if (globalQueryFilter is not null)
        {
            var items = await GetAllAsync(filter, null, asNoTracking, useSplitQuery, cancellationToken).ConfigureAwait(false);
            return [.. items];
        }

        var filteredKey = (typeof(TEntity), useSoftDelete, softDeleteProperty, defaultIncludesKey, filterFingerprint, requestedNoTracking, requestedSplit);
        var filteredDel = CompiledGetAllFiltered.GetOrAdd(filteredKey, _ =>
            BuildCompiledGetAllFiltered(
                useSoftDelete,
                softDeleteProperty,
                defaultIncludeProperties,
                filter,
                requestedNoTracking,
                requestedSplit));

        return await ExecuteWithNotificationsAsync(nameof(GetAllCompiledAsync), filter, async (ct) =>
        {
            var cachePolicy = await ResolveCachePolicyAsync(nameof(GetAllCompiledAsync), ct).ConfigureAwait(false);
            if (cache is not null && IsReadCacheAllowed(nameof(GetAllCompiledAsync), cachePolicy))
            {
                var cacheKey = CacheKeyCompiledFilter($"{nameof(GetAllCompiledAsync)}", filter, requestedNoTracking, requestedSplit, cachePolicy.KeySuffix);
                var options = BuildCacheEntryOptions(cachePolicy);
                return await cache.GetOrCreateAsync(
                    cacheKey,
                    async innerCt =>
                    {
                        var asyncQuery = filteredDel(db);
                        return await asyncQuery.ToListAsync(innerCt).ConfigureAwait(false);
                    },
                    options, ct).ConfigureAwait(false) ?? [];

            }

            var asyncQueryFallback = filteredDel(db);
            return await asyncQueryFallback.ToListAsync(cancellationToken).ConfigureAwait(false);
        }, (e) => new { filter, e.Count }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region Add / Update / Patch / Remove
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return await ExecuteWithNotificationsAsync(nameof(AddAsync), entity, async ct =>
        {
            await set.AddAsync(entity, ct).ConfigureAwait(false);
            await InvalidateCachesAsync(entity, ct).ConfigureAwait(false);
            return entity;
        }, e => entity, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var entityList = entities as IList<TEntity> ?? [.. entities];
        ArgumentOutOfRangeException.ThrowIfLessThan(entityList.Count, 1);
        return await ExecuteWithNotificationsAsync(nameof(AddRangeAsync), entityList, async ct =>
    {
        await set.AddRangeAsync(entityList, ct).ConfigureAwait(false);
        await InvalidateCachesAsync(entityList, ct).ConfigureAwait(false);
        return entityList;
    }, e => e, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return await ExecuteWithNotificationsAsync(nameof(UpdateAsync), entity, async ct =>
        {
            var keyValues = GetPrimaryKeyValues(entity);
            var existing = await MaterializeByIdAsync(new MaterializeByIdCommand(keyValues, false, null, null, [], false, false, ct)).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found for keys {string.Join(',', keyValues)}");
            UpdateEntityProperties(entity, existing);
            await InvalidateCachesAsync(keyValues, ct).ConfigureAwait(false);
            return existing;
        }, e => entity, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentOutOfRangeException.ThrowIfLessThan(entities.Count(), 1);
        return await ExecuteWithNotificationsAsync(nameof(UpdateRangeAsync), entities, async ct =>
        {
            var updated = new List<TEntity>();
            foreach (var entity in entities)
            {
                var u = await UpdateAsync(entity, ct).ConfigureAwait(false);
                updated.Add(u);
            }
            return updated;
        }, e => e, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<TEntity?> PatchInternalAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        var entity = await MaterializeByIdAsync(new MaterializeByIdCommand(keyValues, false, null, null, [], false, false, cancellationToken)).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found for keys {string.Join(',', keyValues!)}");

        var entityType = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' not found in the model.");
        var entry = db.Entry(entity);

        foreach (var update in updates)
        {
            var property = entityType.FindProperty(update.Key)
                ?? throw new InvalidOperationException($"Property '{update.Key}' not found on '{typeof(TEntity).Name}'.");
            var targetProp = entry.Property(property.Name);
            targetProp.CurrentValue = update.Value;
            targetProp.IsModified = true;
        }
        await InvalidateCachesAsync(keyValues!, cancellationToken).ConfigureAwait(false);
        return entity;
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<bool> RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return await ExecuteWithNotificationsAsync(nameof(RemoveAsync), entity, async ct =>
        await RemoveInternalAsync(GetPrimaryKeyValues(entity), false, ct).ConfigureAwait(false),
        removed => new { Entity = entity, Removed = removed }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<bool> RemoveInternalAsync(object?[]? keyValues, bool isSoftDelete = false, CancellationToken cancellationToken = default)
    {
        var entity = await MaterializeByIdAsync(new MaterializeByIdCommand(keyValues, !isSoftDelete, null, null, [], false, false, cancellationToken)).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found for keys {string.Join(',', keyValues!)}");
        if (isSoftDelete && softDeleteEnabled)
        {
            var entry = db.Entry(entity);
            var prop = entry.Property(softDeleteProperty);
            prop.CurrentValue = true;
            prop.IsModified = true;
        }
        else
        {
            set.Remove(entity);
        }
        await InvalidateCachesAsync(keyValues!, cancellationToken).ConfigureAwait(false);
        return true;
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    => await ExecuteWithNotificationsAsync(nameof(RemoveRangeAsync), entities, async ct =>
    {
        var results = new List<bool>();
        foreach (var entity in entities)
        {
            var r = await RemoveAsync(entity, ct).ConfigureAwait(false);
            results.Add(r);
        }
        return results.All(r => r);
    }, removed => new { Entities = entities, Removed = removed }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
    => await ExecuteWithNotificationsAsync(nameof(ExistAsync), filter, async ct =>
    {
        var query = ApplyGlobalFilter(set.AsQueryable());
        if (softDeleteEnabled) query = ApplySoftDelete(query);
        return await query.AnyAsync(filter, ct).ConfigureAwait(false);
    }, e => filter, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    #endregion

    #region Streaming / Query / Paging
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async IAsyncEnumerable<TEntity> StreamAsync(Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool asNoTracking = true,
        bool useSplitQuery = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        await NotifyBeforeAsync(nameof(StreamAsync), filter, cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        var query = await CalculateQueryAsync(new CalculateQueryCommand([filter], [orderBy], null, null, asNoTracking, useSplitQuery, false, includeExpressions)).ConfigureAwait(false);
        await using var enumerator = query.AsAsyncEnumerable().WithCancellation(cancellationToken).GetAsyncEnumerator();
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    exception = ex;
                    throw;
                }
                if (!moved) break;
                yield return enumerator.Current;
            }
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync(nameof(StreamAsync), filter, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<List<TResult>> QueryAsync<TResult>(Expression<Func<TEntity, bool>>? filter,
    Expression<Func<TEntity, TResult>> selector,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool? asNoTracking = null, bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return await ExecuteWithNotificationsAsync(
           operationName: nameof(QueryAsync),
           beforePayload: selector,
           cancellationToken: cancellationToken,
           action: async ct =>
           {
               var query = await CalculateQueryAsync(new CalculateQueryCommand([filter], [orderBy], null, null, asNoTracking, useSplitQuery, false, includeExpressions));
               return await query.Select(selector).ToListAsync(ct);
           },
           successPayloadFactory: items => new { Filter = filter, Count = items?.Count ?? 0 }
       ).ConfigureAwait(false);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<List<TResult>> QueryAsync<TResult>(IKyrolusQuerySpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (specification.Selector is null) throw new ArgumentNullException(nameof(specification), "specification.Selector is required");
        return await ExecuteWithNotificationsAsync($"{nameof(QueryAsync)}.Spec", specification, async ct =>
        {
            var query = await CalculateQueryAsync(new CalculateQueryCommand(
                [specification.Filter], [specification.OrderBy], null, null, specification.AsNoTracking,
                specification is IKyrolusHasSplitQuery split && split.UseSplitQuery, specification.IncludeDeleted,
                specification.Includes)).ConfigureAwait(false);
            var result = await query.Select(specification.Selector).ToListAsync(ct).ConfigureAwait(false);
            return result;
        }, e => e, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<(IReadOnlyList<TResult> Items, int TotalCount)> GetPagedAsync<TResult>(IKyrolusPagedQuerySpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (specification.Selector is null) throw new ArgumentNullException(nameof(specification), "specification.Selector is required");
        var pageNumber = specification.PageNumber;
        var pageSize = specification.PageSize;
        if (pageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(specification), "PageNumber must be greater than 0.");
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(specification), "PageSize must be greater than 0.");

        return await ExecuteWithNotificationsAsync($"{nameof(GetPagedAsync)}.Spec", (specification.PageNumber, specification.PageSize), async ct =>
        {
            var query = await CalculateQueryAsync(new CalculateQueryCommand(
                [specification.Filter], [specification.OrderBy], null, null, specification.AsNoTracking,
                specification is IKyrolusHasSplitQuery split && split.UseSplitQuery, specification.IncludeDeleted,
                specification.Includes)).ConfigureAwait(false);
            var total = await query.CountAsync(ct).ConfigureAwait(false);
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(specification.Selector)
                .ToListAsync(ct).ConfigureAwait(false);
            return (items, total);
        }, (items) => (specification.PageNumber, specification.PageSize), ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<(IReadOnlyList<TEntity> Items, int TotalCount)> GetPagedWithDefaultsAsync<TResult>(IKyrolusPagedQuerySpecification<TEntity, TResult> specification,
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool? asNoTracking = null, bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var effectivePageNumber = specification.PageNumber > 0 ? specification.PageNumber : 1;
        var effectivePageSize = specification.PageSize > 0 ? specification.PageSize : policy.DefaultPageSize ?? 0;
        if (effectivePageSize <= 0) throw new ArgumentOutOfRangeException(nameof(specification), "Page size must be greater than 0 or provided via policy.");

        return await ExecuteWithNotificationsAsync(nameof(GetPagedWithDefaultsAsync), (effectivePageNumber, effectivePageSize), async ct =>
        {
            var explicitIncludes = includeExpressions;
            if (specification.Includes is { Length: > 0 })
                explicitIncludes = explicitIncludes is { Length: > 0 }
                    ? [.. explicitIncludes, .. specification.Includes]
                    : specification.Includes;
            var query = await CalculateQueryAsync(new CalculateQueryCommand([filter, specification.Filter], [orderBy, specification.OrderBy], null, null, asNoTracking, useSplitQuery, specification.IncludeDeleted, explicitIncludes)).ConfigureAwait(false);
            var total = await query.CountAsync(ct).ConfigureAwait(false);
            var items = await query
                .Skip((effectivePageNumber - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .ToListAsync(ct).ConfigureAwait(false);
            return (items, total);
        }, (items) => (effectivePageNumber, effectivePageSize), ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region Bulk-like operations
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<int> ExecuteUpdateAsync(Expression<Func<TEntity, bool>>? filter,
        Action<UpdateSettersBuilder<TEntity>> setPropertyCalls,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setPropertyCalls);
        return await ExecuteWithNotificationsAsync(nameof(ExecuteUpdateAsync), filter, async ct =>
        {
            var effectiveSplit = useSplitQuery ?? splitQueryDefault;
            if (bulkExecutor is not null)
            {
                var count = await bulkExecutor.ExecuteUpdateAsync(filter, setPropertyCalls, effectiveSplit, cancellationToken).ConfigureAwait(false);
                await InvalidateListCachesAsync(cancellationToken).ConfigureAwait(false);
                return count;
            }
            var query = await CalculateQueryAsync(new CalculateQueryCommand([filter], null, null, null, false, effectiveSplit, false));
            var result = await query.ExecuteUpdateAsync(setPropertyCalls, cancellationToken).ConfigureAwait(false);
            await InvalidateListCachesAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }, e => filter, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>>? filter = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    => await ExecuteWithNotificationsAsync(nameof(ExecuteDeleteAsync), filter, async ct =>
    {
        var effectiveSplit = useSplitQuery ?? splitQueryDefault;
        if (bulkExecutor is not null)
        {
            var count = await bulkExecutor.ExecuteDeleteAsync(filter, effectiveSplit, ct).ConfigureAwait(false);
            await InvalidateListCachesAsync(ct).ConfigureAwait(false);
            return count;
        }
        var query = await CalculateQueryAsync(new CalculateQueryCommand([filter], null, null, null, false, effectiveSplit, false)).ConfigureAwait(false);
        var result = await query.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await InvalidateListCachesAsync(ct).ConfigureAwait(false);
        return result;
    }, e => filter, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    #endregion

    #region Try* wrappers
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<RepositoryOperationResult<TEntity>> TryUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return await ExecuteWithNotificationsAsync(nameof(TryUpdateAsync), entity, async ct => await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
                () => UpdateAsync(entity, ct), policy, async ex => await ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionProperty, ct).ConfigureAwait(false),
                ct).ConfigureAwait(false)
        , e => entity, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<RepositoryOperationResult<TEntity>> TryPatchInternalAsync(object?[]? keyValues, string operationName, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfKeyValuesIsNotValid(keyValues, keyPropertyNames.Length);
        ArgumentException.ThrowIfUpdatesIsNotValid(updates);
        return await ExecuteWithNotificationsAsync(operationName, (keyValues, updates), async ct =>
            await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
                    async () => await PatchInternalAsync(keyValues, updates, ct).ConfigureAwait(false),
                    policy,
                    async ex => await ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionProperty, ct).ConfigureAwait(false),
                    ct).ConfigureAwait(false)
        , e => new { KeyValues = keyValues, Updates = updates }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<RepositoryOperationResult<bool>> TryRemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return await ExecuteWithNotificationsAsync(nameof(TryRemoveAsync), entity, async ct =>
            await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
                async () =>
                {
                    await RemoveAsync(entity, ct).ConfigureAwait(false);
                    return true;
                },
                policy,
                async ex => await ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionProperty, ct).ConfigureAwait(false),
                ct).ConfigureAwait(false)
            , removed => new { Entity = entity, Removed = removed }, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected Task<RepositoryOperationResult<bool>> TryRemoveInternalAsync(object?[]? keyValues, string operationName, bool isSoftDelete, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfKeyValuesIsNotValid(keyValues, keyPropertyNames.Length);

        return ExecuteWithNotificationsAsync(operationName, (keyValues, isSoftDelete), async ct =>
        {
            var removeResult = await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
                async () =>
                {
                    await RemoveInternalAsync(keyValues, isSoftDelete, ct).ConfigureAwait(false);
                    return true;
                },
                policy,
                async ex => await ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionProperty, ct).ConfigureAwait(false),
                ct).ConfigureAwait(false);

            if (removeResult.Status == KyrolusRepositoryOperationStatus.Failed &&
                removeResult.Exception is KeyNotFoundException)
                return RepositoryOperationResult<bool>.NotFound();

            return removeResult;
        },
        e => new { KeyValues = keyValues, IsSoftDelete = isSoftDelete }, ex => new { Exception = ex.Message }, cancellationToken);
    }
    #endregion

    #region Soft delete helpers
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected IQueryable<TEntity> BuildBaseQuery(bool includeDeleted)
    {
        var query = ApplyGlobalFilter(set.AsQueryable());
        if (softDeleteEnabled && !includeDeleted)
            query = ApplySoftDelete(query);
        return query;
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<bool> RestoreInternalAsync(object?[]? keyValues, CancellationToken cancellationToken)
    {
        if (!softDeleteEnabled) throw new InvalidOperationException("Soft delete is not enabled for this repository.");

        var entity = await MaterializeByIdAsync(new MaterializeByIdCommand(keyValues, true, null, null, [], false, false, cancellationToken)).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found for keys {string.Join(',', keyValues!)}");

        var entry = db.Entry(entity);
        var prop = entry.Property(softDeleteProperty);
        prop.CurrentValue = false;
        prop.IsModified = true;

        await InvalidateCachesAsync(keyValues, cancellationToken).ConfigureAwait(false);
        return true;
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<RepositoryOperationResult<bool>> TryRestoreInternalAsync(object?[]? keyValues, string operationName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfKeyValuesIsNotValid(keyValues, keyPropertyNames.Length);
        return await ExecuteWithNotificationsAsync(operationName, keyValues, async ct =>
        {
            try
            {
                var restored = await RestoreInternalAsync(keyValues, cancellationToken).ConfigureAwait(false);
                return RepositoryOperationResult<bool>.Success(restored);
            }
            catch (KeyNotFoundException)
            {
                return RepositoryOperationResult<bool>.NotFound();
            }
            catch (Exception ex)
            {
                return RepositoryOperationResult<bool>.Failed(ex);
            }
        }, e => keyValues, ex => new { Exception = ex.Message }, cancellationToken).ConfigureAwait(false);
    }
    #endregion
    #region Query helpers
    //Shoudl be removed
    protected IQueryable<TEntity> QueryIncludingDeleted()
    => BuildBaseQuery(includeDeleted: true);
    protected Expression<Func<TEntity, bool>> DeletedOnlyPredicate()
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var prop = Expression.PropertyOrField(param, softDeleteProperty);
        var body = Expression.Equal(prop, Expression.Constant(true));
        return Expression.Lambda<Func<TEntity, bool>>(body, param);
    }
    private IQueryable<TEntity> ApplyGlobalFilter(IQueryable<TEntity> query)
        => globalQueryFilter is null ? query : globalQueryFilter(query);

    private IQueryable<TEntity> ApplySoftDelete(IQueryable<TEntity> query)
    {
        if (!softDeleteEnabled) return query;
        var param = Expression.Parameter(typeof(TEntity), "e");
        var body = Expression.Not(Expression.PropertyOrField(param, softDeleteProperty));
        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, param);
        return query.Where(lambda);
    }

    private static IQueryable<TEntity> ApplyIncludes(IQueryable<TEntity> query, IEnumerable<Expression<Func<TEntity, object?>>> includes)
    {
        foreach (var include in includes) query = query.Include(include);
        return query;
    }

    protected async Task NotifyBeforeAsync(string op, object? payload, CancellationToken ct)
    {
        if (observer is null) return;
        await observer.OnBeforeAsync(op, payload, ct).ConfigureAwait(false);
    }

    protected async Task NotifyAfterAsync(string op, object? payload, Exception? ex, TimeSpan duration, CancellationToken ct)
    {
        if (observer is null) return;
        await observer.OnAfterAsync(op, payload, duration, ex, ct).ConfigureAwait(false);
    }

    private async Task EnsurePolicyInitializedAsync(CancellationToken ct)
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
        var context = new KyrolusRepositoryPolicyContext(
            typeof(TEntity),
            typeof(TEntity).Name,
            typeof(TDbContext),
            ResolveCacheScope(),
            cacheKeyContext?.TenantId);

        var dynamicPolicy = await policyProvider!.GetPolicyAsync(context, ct).ConfigureAwait(false);
        if (dynamicPolicy is null) return;

        ApplyPolicy(dynamicPolicy);
    }

    private void ApplyPolicy(KyrolusRepositoryPolicy newPolicy)
    {
        policy = newPolicy;
        globalQueryFilter = policy.GetGlobalQueryFilter<TEntity>();
        softDeleteProperty = string.IsNullOrWhiteSpace(policy.SoftDeleteProperty)
            ? "IsDeleted"
            : policy.SoftDeleteProperty!;
        rowVersionProperty = policy.RowVersionProperty;
        splitQueryDefault = policy.UseSplitQueryDefault ?? false;
        asNoTrackingDefault = policy.AsNoTrackingDefault ?? true;
        defaultIncludeMode = policy.DefaultIncludeMode;
        policyDefaultIncludeProperties = policy.GetDefaultIncludeProperties<TEntity>();

        if (!cachePolicyProviderOverride)
            cachePolicyProvider = policy.CachePolicyProvider;
    }

    // private async Task<TResult> ExecuteWithPolicyAsync<TResult>(
    //     Func<KyrolusRepositoryPolicy, Task<TResult>> action,
    //     CancellationToken ct)
    // {
    //     await EnsurePolicyInitializedAsync(ct).ConfigureAwait(false);
    //     return await action(policy).ConfigureAwait(false);
    // }

    protected static Expression<Func<TEntity, bool>> BuildKeyPredicate(object?[]? keyValues, string[] keyNames)
        => KyrolusEFRepositoryBase<TEntity>.GetPrimaryKeyFromKeyValues(keyValues!, keyNames);

    protected string CacheKeyById(string operation, object?[]? keyValues, string? policySuffix)
    {
        var scope = ResolveCacheScope();
        var scopePart = string.IsNullOrWhiteSpace(scope) ? "" : $":scope={Uri.EscapeDataString(scope)}";
        var policyPart = string.IsNullOrWhiteSpace(policySuffix) ? "" : $":policy={Uri.EscapeDataString(policySuffix)}";
        return $"{typeof(TEntity).Name}:op={Uri.EscapeDataString(operation)}:id"
                       + scopePart + policyPart + ":" + BuildKeyValuesFingerprint(keyValues);
    }

    protected string CacheKeyAll(string operation, string? policySuffix)
    {
        var scope = ResolveCacheScope();
        var scopePart = string.IsNullOrWhiteSpace(scope) ? "" : $":scope={Uri.EscapeDataString(scope)}";
        var policyPart = string.IsNullOrWhiteSpace(policySuffix) ? "" : $":policy={Uri.EscapeDataString(policySuffix)}";
        var includesKey = GetDefaultIncludesKey();
        var includesPart = string.IsNullOrWhiteSpace(includesKey) ? "" : $":inc={Uri.EscapeDataString(includesKey)}";
        return $"{typeof(TEntity).Name}:op={Uri.EscapeDataString(operation)}:all"
                        + scopePart + policyPart + includesPart;
    }

    protected string CacheKeyCompiledFilter(string operation, Expression<Func<TEntity, bool>> filter, bool asNoTracking, bool useSplit, string? policySuffix)
    {
        var fingerprint = KyrolusExpressionFingerprint.Build(filter);
        var filterPart = EscapeKeyPart(fingerprint);
        var trackingPart = asNoTracking ? "1" : "0";
        var splitPart = useSplit ? "1" : "0";
        return $"{CacheKeyAll(operation, policySuffix)}:filter={filterPart}:nt={trackingPart}:split={splitPart}";
    }

    private static IEnumerable<string> ExpandInvalidationTemplates(
            IReadOnlyCollection<string>? templates,
            KyrolusInvalidationContext ctx)
    {
        if (templates is null || templates.Count == 0)
            yield break;

        var hasId = !string.IsNullOrWhiteSpace(ctx.IdKey) || !string.IsNullOrWhiteSpace(ctx.IdCompiledKey);

        foreach (var template in templates)
        {
            if (string.IsNullOrWhiteSpace(template))
                continue;

            if (!hasId &&
                (template.Contains("{id}", StringComparison.Ordinal) ||
                template.Contains("{key}", StringComparison.Ordinal)))
                continue;

            var baseExpanded = template
                .Replace("{entity}", ctx.Entity ?? string.Empty, StringComparison.Ordinal)
                .Replace("{tenant}", ctx.Tenant ?? string.Empty, StringComparison.Ordinal)
                .Replace("{scope}", ctx.Scope ?? string.Empty, StringComparison.Ordinal)
                .Replace("{policy}", ctx.PolicySuffix ?? string.Empty, StringComparison.Ordinal)
                .Replace("{key}", ctx.KeyFingerprint ?? string.Empty, StringComparison.Ordinal);

            foreach (var expanded in ExpandSingleTemplate(baseExpanded, ctx))
                yield return expanded;
        }
    }

    private static IEnumerable<string> ExpandSingleTemplate(string baseExpanded, KyrolusInvalidationContext ctx)
    {
        if (baseExpanded.Contains("{all}", StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(ctx.AllKey))
                yield return baseExpanded.Replace("{all}", ctx.AllKey, StringComparison.Ordinal);

            if (!string.IsNullOrWhiteSpace(ctx.AllCompiledKey))
                yield return baseExpanded.Replace("{all}", ctx.AllCompiledKey, StringComparison.Ordinal);

            yield break;
        }

        if (baseExpanded.Contains("{id}", StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(ctx.IdKey))
                yield return baseExpanded.Replace("{id}", ctx.IdKey!, StringComparison.Ordinal);

            if (!string.IsNullOrWhiteSpace(ctx.IdCompiledKey))
                yield return baseExpanded.Replace("{id}", ctx.IdCompiledKey!, StringComparison.Ordinal);

            yield break;
        }

        if (!string.IsNullOrWhiteSpace(baseExpanded))
            yield return baseExpanded;
    }
    private async Task RemoveExtraInvalidationKeysAsync(
            KyrolusInvalidationContext ctx,
            KyrolusCachePolicy policy,
            CancellationToken cancellationToken)
    {
        if (cache is null) return;

        foreach (var key in KyrolusRepositoryAsync<TDbContext, TEntity, TKey>.ExpandInvalidationTemplates(policy.ExtraInvalidationKeys, ctx))
            await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);

        foreach (var pattern in KyrolusRepositoryAsync<TDbContext, TEntity, TKey>.ExpandInvalidationTemplates(policy.ExtraInvalidationKeyPatterns, ctx))
            await cache.RemoveKeysByPatternAsync(pattern, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildKeyValuesFingerprint(object?[]? keyValues)
        => string.Join("|", keyValues!.Select((v, i) => $"{i}={EscapeKeyPart(v)}"));

    private static string EscapeKeyPart(object? value)
    {
        var s = value switch
        {
            null => "null",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
            _ => value.ToString() ?? "null"
        };
        return Uri.EscapeDataString(s);
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

    protected async ValueTask<KyrolusCachePolicy> ResolveCachePolicyAsync(string operation, CancellationToken ct)
    {
        var effective = new KyrolusCachePolicy(
            AbsoluteExpirationRelativeToNow: cacheTtl,
            Enabled: enableCaching);
        var staticPolicy = policy?.GetCachePolicy<TEntity>();
        effective = MergeCachePolicy(effective, staticPolicy);

        if (cachePolicyProvider is not null)
        {
            var context = new KyrolusRepositoryCachePolicyContext(
                typeof(TEntity),
                typeof(TEntity).Name,
                operation,
                ResolveCacheScope(),
                cacheKeyContext?.TenantId);
            var dynamicPolicy = await cachePolicyProvider.GetPolicyAsync(context, ct).ConfigureAwait(false);
            effective = MergeCachePolicy(effective, dynamicPolicy);
        }

        return effective;
    }

    protected static bool IsCacheEnabled(KyrolusCachePolicy policy)
        => policy.Enabled.GetValueOrDefault();

    private string? ResolveCacheScope()
    {
        if (cacheKeyContext is null) return null;
        if (!string.IsNullOrWhiteSpace(cacheKeyContext.ScopeKey)) return cacheKeyContext.ScopeKey;
        if (!string.IsNullOrWhiteSpace(cacheKeyContext.TenantId)) return $"tenant={cacheKeyContext.TenantId}";
        return null;
    }

    private string? ResolveCacheRegion()
    {
        if (cacheKeyContext is null) return null;
        if (!string.IsNullOrWhiteSpace(cacheKeyContext.Region)) return cacheKeyContext.Region;
        return ResolveCacheScope();
    }

    protected KyrolusCacheEntryOptions BuildCacheEntryOptions(KyrolusCachePolicy policy)
    {
        return new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = policy.AbsoluteExpirationRelativeToNow,
            SlidingExpiration = policy.SlidingExpiration,
            Jitter = policy.Jitter,
            NegativeExpirationRelativeToNow = policy.NegativeCacheTtl,
            Region = ResolveCacheRegion(),
            TenantId = cacheKeyContext?.TenantId
        };
    }
    protected static IQueryable<TEntity> ApplyCompiledQueryInternal(TDbContext ctx, bool noTrack, bool split)
    {
        IQueryable<TEntity> query = ctx.Set<TEntity>();
        if (noTrack) query = query.AsNoTracking();
        if (split) query = query.AsSplitQuery();
        return query;
    }

    protected async Task<IReadOnlyList<TEntity>> GetAllInternalAsync(GetAllCommand cmd)
    {
        return await ExecuteWithNotificationsAsync(cmd.OperationName, cmd.Filter, async (ct) =>
        {
            var query = await CalculateQueryAsync(new CalculateQueryCommand([cmd.Filter], [cmd.OrderBy],
                        cmd.IncludeProperties, cmd.IncludeGraph, cmd.AsNoTracking, cmd.UseSplitQuery, cmd.IncludeDeleted, cmd.IncludeExpressions)).ConfigureAwait(false);
            if (cmd.DeletedOnly)
                query = query.Where(e => Microsoft.EntityFrameworkCore.EF.Property<bool>(e, softDeleteProperty));
            var cachePolicy = await ResolveCachePolicyAsync(cmd.OperationName, ct).ConfigureAwait(false);
            if (cache is not null && globalQueryFilter is null
                && cmd.Filter is null && cmd.OrderBy is null
                && cmd.CachePredicate && IsReadCacheAllowed(cmd.OperationName, cachePolicy))
            {
                var cacheKey = CacheKeyAll(cmd.OperationName, cachePolicy.KeySuffix);
                AddIncludeDeeltedKey(ref cacheKey, cmd.IncludeDeleted);
                if (cmd.DeletedOnly) cacheKey += ":delonly=1";
                var options = BuildCacheEntryOptions(cachePolicy);
                return await cache.GetOrCreateAsync(
                    cacheKey, async innerCt => await query.ToListAsync(innerCt).ConfigureAwait(false),
                    options, ct).ConfigureAwait(false);
            }
            return await query.ToListAsync(ct).ConfigureAwait(false);
        }, (e) => new { cmd.Filter, e.Count }, ex => ex.Message, cmd.CancellationToken);
    }
    private static void AddIncludeDeeltedKey(ref string cacheKey, bool includeDeleted)
    {
        if (includeDeleted)
            cacheKey += ":incdel=1";
    }

    private static IQueryable<TEntity> ApplyIncludeProperties(IQueryable<TEntity> query, List<string>? includeProperties)
    {
        if (includeProperties is not { Count: > 0 }) return query;
        foreach (var includeProperty in includeProperties)
        {
            if (string.IsNullOrWhiteSpace(includeProperty)) continue;
            query = query.Include(includeProperty);
        }
        return query;
    }

    private static bool HasExplicitIncludes(List<string>? includeProperties, IncludeGraph<TEntity>? includeGraph, Expression<Func<TEntity, object?>>[]? includeExpressions)
    {
        var hasIncludeProperties = includeProperties is { Count: > 0 } && includeProperties.Any(p => !string.IsNullOrWhiteSpace(p));
        var hasGraphIncludes = includeGraph is { Includes.Count: > 0 };
        var hasExpressionIncludes = includeExpressions is { Length: > 0 };
        return hasIncludeProperties || hasGraphIncludes || hasExpressionIncludes;
    }

    private static List<string>? CleanIncludeList(List<string>? includeProperties)
    {
        if (includeProperties is null || includeProperties.Count == 0) return null;
        var cleaned = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var includeProperty in includeProperties)
        {
            if (string.IsNullOrWhiteSpace(includeProperty)) continue;
            var trimmed = includeProperty.Trim();
            if (trimmed.Length == 0) continue;
            if (seen.Add(trimmed))
                cleaned.Add(trimmed);
        }

        return cleaned.Count == 0 ? null : cleaned;
    }

    private List<string>? ResolvePolicyIncludes(List<string>? includeProperties, IncludeGraph<TEntity>? includeGraph, Expression<Func<TEntity, object?>>[]? includeExpressions)
    {
        if (policyDefaultIncludeProperties.Length == 0) return null;

        var hasExplicitIncludes = HasExplicitIncludes(includeProperties, includeGraph, includeExpressions);
        if (defaultIncludeMode == KyrolusDefaultIncludeMode.Replace && hasExplicitIncludes)
            return null;

        // helper to normalize and dedupe a string sequence
        static List<string> NormalizeDistinct(IEnumerable<string?>? inputs)
        {
            var list = new List<string>();
            if (inputs is null) return list;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in inputs)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                var t = s.Trim();
                if (t.Length == 0) continue;
                if (seen.Add(t)) list.Add(t);
            }
            return list;
        }

        var result = NormalizeDistinct(policyDefaultIncludeProperties);
        if (result.Count == 0) return null;

        if (defaultIncludeMode == KyrolusDefaultIncludeMode.Merge && includeProperties is { Count: > 0 })
        {
            var explicitSet = new HashSet<string>(NormalizeDistinct(includeProperties), StringComparer.Ordinal);
            if (explicitSet.Count > 0)
            {
                result = [.. result.Where(p => !explicitSet.Contains(p))];
                if (result.Count == 0) return null;
            }
        }

        return result;
    }

    private List<string>? ResolveIncludeProperties(List<string>? includeProperties, IncludeGraph<TEntity>? includeGraph, Expression<Func<TEntity, object?>>[]? includeExpressions)
    {
        var cleanedExplicit = CleanIncludeList(includeProperties);
        var hasExplicitIncludes = HasExplicitIncludes(includeProperties, includeGraph, includeExpressions);

        if (defaultIncludeMode == KyrolusDefaultIncludeMode.Replace && hasExplicitIncludes)
            return cleanedExplicit;

        var policyIncludes = ResolvePolicyIncludes(includeProperties, includeGraph, includeExpressions);
        if (policyIncludes is null || policyIncludes.Count == 0)
            return cleanedExplicit;

        if (cleanedExplicit is null || cleanedExplicit.Count == 0)
            return policyIncludes;

        var merged = new List<string>(policyIncludes);
        var seen = new HashSet<string>(policyIncludes, StringComparer.Ordinal);
        merged.AddRange(cleanedExplicit.Where(seen.Add));
        return merged.Count == 0 ? null : merged;
    }


    private static Func<TDbContext, IAsyncEnumerable<TEntity>> BuildCompiledGetAllFiltered(
        bool useSoftDelete,
        string softDeleteProperty,
        string[] defaultIncludeProperties,
        Expression<Func<TEntity, bool>> filter,
        bool asNoTracking,
        bool useSplitQuery)
    {
        try
        {
            var ctxParam = Expression.Parameter(typeof(TDbContext), "ctx");

            var setMethod = typeof(DbContext).GetMethods();
            var setGeneric = setMethod.Single(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethod && m.GetParameters().Length == 0)
                .MakeGenericMethod(typeof(TEntity));
            Expression query = Expression.Call(ctxParam, setGeneric);

            if (useSoftDelete)
            {
                var entityParam = Expression.Parameter(typeof(TEntity), "e");
                var efPropertyMethod = typeof(Microsoft.EntityFrameworkCore.EF).GetMethod(nameof(Microsoft.EntityFrameworkCore.EF.Property))!
                    .MakeGenericMethod(typeof(bool));
                var propAccess = Expression.Call(efPropertyMethod, entityParam, Expression.Constant(softDeleteProperty));
                var predicate = Expression.Lambda<Func<TEntity, bool>>(Expression.Not(propAccess), entityParam);
                var whereMethod = GetQueryableWhereMethod()
                    .MakeGenericMethod(typeof(TEntity));
                query = Expression.Call(whereMethod, query, Expression.Quote(predicate));
            }

            if (defaultIncludeProperties.Length > 0)
            {
                var includeStringMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                    .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType == typeof(string))
                    .MakeGenericMethod(typeof(TEntity));

                foreach (var includeProperty in defaultIncludeProperties)
                {
                    if (string.IsNullOrWhiteSpace(includeProperty)) continue;
                    query = Expression.Call(includeStringMethod, query, Expression.Constant(includeProperty));
                }
            }

            var filterWhere = GetQueryableWhereMethod()
                .MakeGenericMethod(typeof(TEntity));
            var filterExpr = Expression.Quote(filter);
            query = Expression.Call(filterWhere, query, filterExpr);

            var asQueryable = query.Type == typeof(IQueryable<TEntity>)
                ? query
                : Expression.Convert(query, typeof(IQueryable<TEntity>));

            if (asNoTracking)
            {
                var asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                    .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking) && m.GetParameters().Length == 1)
                    .MakeGenericMethod(typeof(TEntity));
                asQueryable = Expression.Call(asNoTrackingMethod, asQueryable);
            }

            if (useSplitQuery)
            {
                var asSplitMethod = typeof(RelationalQueryableExtensions).GetMethods()
                    .Single(m => m.Name == nameof(RelationalQueryableExtensions.AsSplitQuery) && m.GetParameters().Length == 1)
                    .MakeGenericMethod(typeof(TEntity));
                asQueryable = Expression.Call(asSplitMethod, asQueryable);
            }

            var lambda = Expression.Lambda<Func<TDbContext, IQueryable<TEntity>>>(
                asQueryable,
                ctxParam);
            return Microsoft.EntityFrameworkCore.EF.CompileAsyncQuery(lambda);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build compiled GetAll query.", ex);
        }
    }

    private static System.Reflection.MethodInfo GetQueryableWhereMethod()
        => typeof(Queryable).GetMethods()
            .Single(m => m.Name == nameof(Queryable.Where)
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.IsGenericType
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].IsGenericType
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>));

    private string GetDefaultIncludesKey()
    {
        if (policyDefaultIncludeProperties.Length == 0) return string.Empty;
        return string.Join("|", policyDefaultIncludeProperties);
    }
    #endregion

    #region Protected key helpers
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<TEntity?> GetByIdInternalAsync(GetByIdCommand cmd)
    {
        ArgumentException.ThrowIfKeyValuesIsNotValid(cmd.KeyValues, keyPropertyNames.Length);
        return await ExecuteWithNotificationsAsync(cmd.OperationName, cmd.KeyValues, async (ct) =>
                {
                    var cachePolicy = await ResolveCachePolicyAsync(cmd.OperationName, cmd.CancellationToken).ConfigureAwait(false);
                    if (cache is not null && cmd.CachePredicate && IsReadCacheAllowed(cmd.OperationName, cachePolicy))
                    {
                        var cacheKey = CacheKeyById(cmd.OperationName, cmd.KeyValues!, cachePolicy.KeySuffix);
                        AddIncludeDeeltedKey(ref cacheKey, cmd.IncludeDeleted);

                        var options = BuildCacheEntryOptions(cachePolicy);
                        return await cache.GetOrCreateAsync(
                            cacheKey,
                            async ct => await MaterializeByIdAsync(
                                new MaterializeByIdCommand(cmd.KeyValues,
                                cmd.IncludeDeleted, cmd.IncludeProperties,
                                cmd.IncludeGraph, cmd.IncludeExpressions!, cmd.AsNoTracking,
                                cmd.UseSplitQuery, cmd.CancellationToken)
                            ).ConfigureAwait(false),
                            options,
                            cmd.CancellationToken).ConfigureAwait(false);
                    }

                    return await MaterializeByIdAsync(new MaterializeByIdCommand(cmd.KeyValues,
                                    cmd.IncludeDeleted, cmd.IncludeProperties,
                                    cmd.IncludeGraph, cmd.IncludeExpressions!, cmd.AsNoTracking,
                                    cmd.UseSplitQuery, cmd.CancellationToken)).ConfigureAwait(false);
                }, (c) => cmd.KeyValues, ex => ex.Message, cmd.CancellationToken);
    }

    private IQueryable<TEntity> ApplyAllIncludes(
        IQueryable<TEntity> query,
        List<string>? includeProperties,
        IncludeGraph<TEntity>? includeGraph,
        Expression<Func<TEntity, object?>>[]? includeExpressions)
    {
        var effectiveIncludeProperties = ResolveIncludeProperties(includeProperties, includeGraph, includeExpressions);
        query = ApplyIncludeProperties(query, effectiveIncludeProperties);

        if (includeGraph?.Includes is { Count: > 0 } graphIncludes)
            query = ApplyIncludes(query, graphIncludes);

        if (includeExpressions is { Length: > 0 })
            query = ApplyIncludes(query, includeExpressions);

        return query;
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<TEntity?> MaterializeByIdAsync(
                MaterializeByIdCommand cmd)
    {
        var query = await CalculateQueryAsync(new CalculateQueryCommand
        {
            IncludeDeleted = cmd.IncludeDeleted,
            AsNoTracking = cmd.AsNoTracking,
            UseSplitQuery = cmd.UseSplitQuery,
            IncludeProperties = cmd.IncludeProperties,
            IncludeGraph = cmd.IncludeGraph,
            IncludeExpressions = cmd.IncludeExpressions,
        });

        var predicate = BuildKeyPredicate(cmd.KeyValues, keyPropertyNames);
        return await query.FirstOrDefaultAsync(predicate, cmd.CancellationToken).ConfigureAwait(false);
    }

    #endregion
    #region Helpers
    protected record CalculateQueryCommand(Expression<Func<TEntity, bool>>?[]? Filters = null,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>?[]? OrderBy = null,
    List<string>? IncludeProperties = null, IncludeGraph<TEntity>? IncludeGraph = null, bool? AsNoTracking = null, bool? UseSplitQuery = null, bool IncludeDeleted = false,
    params Expression<Func<TEntity, object?>>[]? IncludeExpressions);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    private async Task<IQueryable<TEntity>> CalculateQueryAsync(CalculateQueryCommand cmd)
    {
        var effectiveAsNoTracking = cmd.AsNoTracking ?? policy?.AsNoTrackingDefault ?? true;
        var effectiveSplit = cmd.UseSplitQuery ?? splitQueryDefault;
        IQueryable<TEntity> query = BuildBaseQuery(cmd.IncludeDeleted);
        query = ApplyAllIncludes(query, cmd.IncludeProperties, cmd.IncludeGraph, cmd.IncludeExpressions);
        if (effectiveAsNoTracking)
            query = query.AsNoTracking();
        if (effectiveSplit)
            query = query.AsSplitQuery();
        cmd.Filters?.Where(f => f is not null).ToList().ForEach(f => query = query.Where(f!));
        cmd.OrderBy?.Where(o => o is not null).ToList().ForEach(o => query = o!(query));
        return query;
    }
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<TResult> ExecuteWithNotificationsAsync<TResult>(string operationName,
            object? beforePayload,
            Func<CancellationToken, Task<TResult>> action,
            Func<TResult, object?>? successPayloadFactory = null,
            Func<Exception, object?>? errorPayloadFactory = null, CancellationToken cancellationToken = default)
    {
        await EnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        await NotifyBeforeAsync(operationName, beforePayload, cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            sw.Stop();
            var afterPayload = successPayloadFactory is null
                ? beforePayload
                : successPayloadFactory(result);
            await NotifyAfterAsync(operationName, afterPayload, null, sw.Elapsed, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var afterPayload = errorPayloadFactory is null
                ? beforePayload
                : errorPayloadFactory(ex);
            await NotifyAfterAsync(operationName, afterPayload, ex, sw.Elapsed, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
    private string[] GetPrimaryKeyNames()
    {
        var entityType = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' not found in the model.");
        var pk = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Primary key not found for entity type '{typeof(TEntity).Name}'.");
        return [.. pk.Properties.Select(p => p.Name)];
    }

    private object?[] GetPrimaryKeyValues(TEntity entity)
        => [.. keyPropertyNames.Select(k => entity.GetType().GetProperty(k)?.GetValue(entity))];

    private void UpdateEntityProperties(TEntity source, TEntity target)
    {
        var sourceEntry = db.Entry(source);
        var targetEntry = db.Entry(target);
        foreach (var targetProp in targetEntry.Properties)
        {
            var property = targetProp.Metadata;
            if (property.IsPrimaryKey() || property.IsShadowProperty())
                continue;

            var sourceProp = sourceEntry.Property(property.Name);
            if (Equals(targetProp.CurrentValue, sourceProp.CurrentValue))
                continue;

            targetProp.CurrentValue = sourceProp.CurrentValue;
            targetProp.IsModified = true;
        }
    }

    private async Task InvalidateCachesAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (cache is null) return;
        var keyValues = GetPrimaryKeyValues(entity);
        await InvalidateCachesAsync(keyValues, cancellationToken).ConfigureAwait(false);
    }

    private async Task InvalidateCachesAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        if (cache is null) return;
        foreach (var entity in entities)
        {
            var keyValues = GetPrimaryKeyValues(entity);
            await InvalidateCachesAsync(keyValues, cancellationToken).ConfigureAwait(false);
        }
    }
    private async Task InvalidateCachesAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
    {
        if (cache is null) return;

        var (allPolicy, allCompiledPolicy) = await InvalidateListCachesAsync(cancellationToken).ConfigureAwait(false);
        var byIdPolicy = await ResolveCachePolicyAsync("GetByIdAsync", cancellationToken).ConfigureAwait(false);
        var byIdCompiledPolicy = await ResolveCachePolicyAsync("GetByIdCompiledAsync", cancellationToken).ConfigureAwait(false);
        var scope = ResolveCacheScope();
        var tenant = cacheKeyContext?.TenantId;
        var hasKeys = keyValues is { Length: > 0 };
        var keyFingerprint = hasKeys ? BuildKeyValuesFingerprint(keyValues) : null;

        var ctx = new KyrolusInvalidationContext(
            Entity: typeof(TEntity).Name,
            Tenant: tenant,
            Scope: scope,
            PolicySuffix: allPolicy.KeySuffix,
            KeyFingerprint: keyFingerprint,

            AllKey: CacheKeyAll(nameof(GetAllAsync), allPolicy.KeySuffix),
            AllCompiledKey: CacheKeyAll(nameof(GetAllCompiledAsync), allCompiledPolicy.KeySuffix),

            IdKey: (hasKeys && IsReadCacheAllowed("GetByIdAsync", byIdPolicy))
                ? CacheKeyById("GetByIdAsync", keyValues, byIdPolicy.KeySuffix)
                : null,

            IdCompiledKey: (hasKeys && IsReadCacheAllowed("GetByIdCompiledAsync", byIdCompiledPolicy))
                ? CacheKeyById("GetByIdCompiledAsync", keyValues, byIdCompiledPolicy.KeySuffix)
                : null
        );
        if (IsCacheEnabled(allPolicy))
            await RemoveExtraInvalidationKeysAsync(ctx, allPolicy, cancellationToken).ConfigureAwait(false);

        if (ctx.IdKey is not null)
            await cache.RemoveAsync(ctx.IdKey, cancellationToken).ConfigureAwait(false);
        if (ctx.IdCompiledKey is not null)
            await cache.RemoveAsync(ctx.IdCompiledKey, cancellationToken).ConfigureAwait(false);

        if (softDeleteEnabled && hasKeys)
        {
            var includeDeletedPolicy = await ResolveCachePolicyAsync("GetByIdIncludingDeletedAsync", cancellationToken).ConfigureAwait(false);
            if (IsCacheEnabled(includeDeletedPolicy))
            {
                var includeDeletedKey = CacheKeyById("GetByIdIncludingDeletedAsync", keyValues, includeDeletedPolicy.KeySuffix);
                AddIncludeDeeltedKey(ref includeDeletedKey, true);
                await cache.RemoveAsync(includeDeletedKey, cancellationToken).ConfigureAwait(false);
            }
        }
    }
    protected bool IsReadCacheAllowed(string operation, KyrolusCachePolicy cachePolicy)
    {
        if (!IsCacheEnabled(cachePolicy))
            return false;

        var allowed = policy?.GetCacheReadOperations<TEntity>()
                    ?? KyrolusCacheReadOperations.SafeDefaults;

        var op = KyrolusEFRepositoryBase<TEntity>.MapReadOperation(operation);
        return (allowed & op) != 0;
    }


    private async Task<(KyrolusCachePolicy AllPolicy, KyrolusCachePolicy AllCompiledPolicy)> InvalidateListCachesAsync(CancellationToken cancellationToken = default)
    {
        if (cache is null) return (new KyrolusCachePolicy(Enabled: false), new KyrolusCachePolicy(Enabled: false));
        var filterKey = ":filter=*";
        var allPolicy = await ResolveCachePolicyAsync(nameof(GetAllAsync), cancellationToken).ConfigureAwait(false);
        if (IsReadCacheAllowed(nameof(GetAllAsync), allPolicy))
        {
            var allKey = CacheKeyAll(nameof(GetAllAsync), allPolicy.KeySuffix);
            await cache.RemoveAsync(allKey, cancellationToken).ConfigureAwait(false);
            await cache.RemoveKeysByPatternAsync(allKey + filterKey, cancellationToken).ConfigureAwait(false);
        }
        var allIncludingDeletedPolicy = await ResolveCachePolicyAsync("GetAllIncludingDeletedAsync", cancellationToken).ConfigureAwait(false);
        if (IsReadCacheAllowed("GetAllIncludingDeletedAsync", allIncludingDeletedPolicy))
        {
            var allIncludingDeletedKey = CacheKeyAll("GetAllIncludingDeletedAsync", allIncludingDeletedPolicy.KeySuffix);
            AddIncludeDeeltedKey(ref allIncludingDeletedKey, true);

            await cache.RemoveAsync(allIncludingDeletedKey, cancellationToken).ConfigureAwait(false);
            await cache.RemoveKeysByPatternAsync(allIncludingDeletedKey + filterKey, cancellationToken).ConfigureAwait(false);
        }
        var deletedOnlyPolicy = await ResolveCachePolicyAsync("GetDeletedOnlyAsync", cancellationToken).ConfigureAwait(false);
        if (IsReadCacheAllowed("GetDeletedOnlyAsync", deletedOnlyPolicy))
        {
            var deletedOnlyKey = CacheKeyAll("GetDeletedOnlyAsync", deletedOnlyPolicy.KeySuffix) + ":incdel=1:delonly=1";
            await cache.RemoveAsync(deletedOnlyKey, cancellationToken).ConfigureAwait(false);
            await cache.RemoveKeysByPatternAsync(deletedOnlyKey + filterKey, cancellationToken).ConfigureAwait(false);
        }
        var allCompiledPolicy = await ResolveCachePolicyAsync(nameof(GetAllCompiledAsync), cancellationToken).ConfigureAwait(false);
        if (IsReadCacheAllowed(nameof(GetAllCompiledAsync), allCompiledPolicy))
        {
            var allCompiledKey = CacheKeyAll(nameof(GetAllCompiledAsync), allCompiledPolicy.KeySuffix);
            await cache.RemoveAsync(allCompiledKey, cancellationToken).ConfigureAwait(false);
            await cache.RemoveKeysByPatternAsync(allCompiledKey + filterKey, cancellationToken).ConfigureAwait(false);
        }
        return (allPolicy, allCompiledPolicy);
    }
    #endregion
}
