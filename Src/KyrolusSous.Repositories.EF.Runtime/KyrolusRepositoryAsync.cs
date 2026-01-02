using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using System.Runtime.CompilerServices;
using KyrolusSous.Repositories.EF.Abstractions;
using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Policy;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Repositories.EF.Runtime;

/// <summary>
/// Generic repository implementation that mirrors the generated repository features:
/// observer hooks, optional caching, optional global filters, bulk fallbacks, compiled queries,
/// paging with specifications, and full cancellation token flow.
/// </summary>
public class KyrolusRepositoryAsync<
    TDbContext,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity,
    TKey> :
    IKyrolusRepositoryAsync<TDbContext, TEntity, TKey>
    where TDbContext : DbContext
    where TEntity : class
{
    protected readonly TDbContext db;
    protected readonly DbSet<TEntity> set;
    protected readonly KyrolusRepositoryPolicy policy;
    protected readonly IKyrolusRepositoryObserver? observer;
    protected readonly IKyrolusBulkExecutor<TEntity>? bulkExecutor;
    protected readonly ICacheProvider? cache;
    protected readonly bool enableCaching;
    protected readonly TimeSpan? cacheTtl;
    protected readonly string cacheAllKey;
    protected readonly Func<IQueryable<TEntity>, IQueryable<TEntity>>? globalQueryFilter;
    protected readonly bool softDeleteEnabled;
    protected readonly string softDeleteProperty;
    protected readonly string? rowVersionProperty;
    protected readonly bool splitQueryDefault;
    protected readonly bool asNoTrackingDefault;
    protected readonly string[] keyPropertyNames;
    protected static readonly ConcurrentDictionary<Type, Func<TDbContext, TKey, IAsyncEnumerable<TEntity>>> CompiledById = new();
    private static readonly ConcurrentDictionary<(Type Type, bool AsNoTracking, bool UseSplit, bool SoftDelete, string SoftDeleteProperty, string DefaultIncludesKey), Func<TDbContext, IAsyncEnumerable<TEntity>>> CompiledGetAll = new();
    protected virtual Expression<Func<TEntity, object?>>[] DefaultIncludes => Array.Empty<Expression<Func<TEntity, object?>>>();

    public KyrolusRepositoryAsync(
        TDbContext db,
        KyrolusRepositoryPolicy? policy = null,
        IKyrolusRepositoryObserver? observer = null,
        IKyrolusBulkExecutor<TEntity>? bulkExecutor = null,
        ICacheProvider? cache = null,
        bool enableCaching = false,
        int? cacheTtlSeconds = null)
    {
        this.db = db ?? throw new ArgumentNullException(nameof(db));
        set = db.Set<TEntity>();
        this.policy = policy ?? KyrolusRepositoryPolicy.Default;
        this.observer = observer;
        this.bulkExecutor = bulkExecutor;
        this.cache = cache;
        this.enableCaching = enableCaching && cache is not null;
        cacheTtl = cacheTtlSeconds is > 0 ? TimeSpan.FromSeconds(cacheTtlSeconds.Value) : null;
        cacheAllKey = $"{typeof(TEntity).Name}:all:compiled";
        globalQueryFilter = this.policy.GlobalQueryFilter as Func<IQueryable<TEntity>, IQueryable<TEntity>>;
        softDeleteEnabled = this.policy.EnableSoftDeleteDefault ?? false;
        softDeleteProperty = string.IsNullOrWhiteSpace(this.policy.SoftDeleteProperty)
            ? "IsDeleted"
            : this.policy.SoftDeleteProperty!;
        rowVersionProperty = this.policy.RowVersionProperty;
        splitQueryDefault = this.policy.UseSplitQueryDefault ?? false;
        asNoTrackingDefault = this.policy.AsNoTrackingDefault ?? true;
        keyPropertyNames = GetPrimaryKeyNames();
    }

    #region Query helpers
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

    private IQueryable<TEntity> ApplyIncludes(IQueryable<TEntity> query, IEnumerable<Expression<Func<TEntity, object?>>> includes)
    {
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
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

    protected static Expression<Func<TEntity, bool>> BuildKeyPredicate(object?[] keyValues, string[] keyNames)
        => KyrolusEFRepositoryBase<TEntity>.GetPrimaryKeyFromKeyValues(keyValues, keyNames);

    protected string CacheKeyById(object id) => $"{typeof(TEntity).Name}:id:{id}";

    protected static IQueryable<TEntity> ApplyCompiledQueryInternal(TDbContext ctx, bool track, bool split)
    {
        IQueryable<TEntity> query = ctx.Set<TEntity>();
        if (track) query = query.AsNoTracking();
        if (split) query = query.AsSplitQuery();
        return query;
    }
    #endregion

    private static Func<TDbContext, IAsyncEnumerable<TEntity>> BuildCompiledGetAll(bool asNoTracking, bool useSplit, bool useSoftDelete, string softDeleteProperty, Expression<Func<TEntity, object?>>[] defaultIncludes)
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
            var whereMethod = typeof(Queryable).GetMethods()
                .Single(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity));
            query = Expression.Call(whereMethod, query, predicate);
        }

        if (defaultIncludes.Length > 0)
        {
            var includeMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include) && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity), typeof(object));
            foreach (var include in defaultIncludes)
            {
                var includeConst = Expression.Constant(include, typeof(Expression<Func<TEntity, object?>>));
                query = Expression.Call(includeMethod, query, includeConst);
            }
        }

        if (asNoTracking)
        {
            var asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking) && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(TEntity));
            query = Expression.Call(asNoTrackingMethod, query);
        }

        if (useSplit)
        {
            var asSplitMethod = typeof(RelationalQueryableExtensions).GetMethods()
                .Single(m => m.Name == nameof(RelationalQueryableExtensions.AsSplitQuery) && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(TEntity));
            query = Expression.Call(asSplitMethod, query);
        }

        var lambda = Expression.Lambda<Func<TDbContext, IQueryable<TEntity>>>(query, ctxParam);
        return Microsoft.EntityFrameworkCore.EF.CompileAsyncQuery(lambda);
    }

    private string GetDefaultIncludesKey()
    {
        var includes = DefaultIncludes;
        if (includes.Length == 0) return string.Empty;
        return string.Join("|", includes.Select(i => i.ToString()));
    }

    #region Protected key helpers
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<TEntity?> GetByIdInternalAsync(object?[] keyValues, bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("GetByIdAsync", keyValues, cancellationToken).ConfigureAwait(false);
        try
        {
            if (enableCaching && cache is not null && (includeExpressions == null || includeExpressions.Length == 0))
            {
                var cacheKey = CacheKeyById(string.Join('|', keyValues.Select(v => v ?? "null")));
                return await cache.GetOrSetAsync(cacheKey,
                    async ct => await MaterializeByIdAsync(keyValues, asNoTracking, useSplitQuery, [], ct).ConfigureAwait(false),
                    cacheTtl,
                    cancellationToken).ConfigureAwait(false);
            }

            return await MaterializeByIdAsync(keyValues, asNoTracking, useSplitQuery, includeExpressions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("GetByIdAsync", keyValues, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    protected Expression<Func<TEntity, object?>>[] BuildIncludes(List<string>? includeProperties, IncludeGraph<TEntity>? includeGraph, params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        var includes = new List<Expression<Func<TEntity, object?>>>();
        var converted = KyrolusEFRepositoryBase<TEntity>.ConvertIncludePropertiesToExpressions(includeProperties);
        if (converted is not null) includes.AddRange(converted);
        if (includeGraph?.Includes is not null) includes.AddRange(includeGraph.Includes);
        if (includeExpressions is not null && includeExpressions.Length > 0) includes.AddRange(includeExpressions);
        return [.. includes];
    }

    protected async Task<TEntity?> GetByIdInternalWithStringIncludesAsync(object?[] keyValues, List<string> includeProperties,
        IncludeGraph<TEntity>? includeGraph, bool? asNoTracking, bool? useSplitQuery, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("GetByIdAsync", keyValues, cancellationToken).ConfigureAwait(false);
        try
        {
            var query = ApplyGlobalFilter(set.AsQueryable());
            if (softDeleteEnabled) query = ApplySoftDelete(query);
            if (asNoTracking ?? asNoTrackingDefault) query = query.AsNoTracking();
            if (useSplitQuery ?? splitQueryDefault) query = query.AsSplitQuery();
            var defaultIncludes = DefaultIncludes;
            if (defaultIncludes.Length > 0)
            {
                query = ApplyIncludes(query, defaultIncludes);
            }

            foreach (var includeProperty in includeProperties)
            {
                if (string.IsNullOrWhiteSpace(includeProperty)) continue;
                query = query.Include(includeProperty);
            }
            if (includeGraph?.Includes is not null && includeGraph.Includes.Count > 0)
            {
                query = ApplyIncludes(query, includeGraph.Includes);
            }

            var predicate = BuildKeyPredicate(keyValues, keyPropertyNames);
            return await query.FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("GetByIdAsync", keyValues, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    protected async Task<TEntity?> MaterializeByIdAsync(object?[] keyValues, bool? asNoTracking, bool? useSplitQuery, Expression<Func<TEntity, object?>>[] includeExpressions, CancellationToken ct)
    {
        var query = ApplyGlobalFilter(set.AsQueryable());
        if (softDeleteEnabled) query = ApplySoftDelete(query);
        if (asNoTracking ?? asNoTrackingDefault) query = query.AsNoTracking();
        if (useSplitQuery ?? splitQueryDefault) query = query.AsSplitQuery();
        var defaultIncludes = DefaultIncludes;
        if (defaultIncludes.Length > 0)
        {
            query = ApplyIncludes(query, defaultIncludes);
        }
        if (includeExpressions is not null && includeExpressions.Length > 0)
            query = ApplyIncludes(query, includeExpressions);

        var predicate = BuildKeyPredicate(keyValues, keyPropertyNames);
        return await query.FirstOrDefaultAsync(predicate, ct).ConfigureAwait(false);
    }
    #endregion

    #region GetAll
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        List<string>? includeProperties = null, IncludeGraph<TEntity>? includeGraph = null, bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("GetAllAsync", filter, cancellationToken).ConfigureAwait(false);
        try
        {
            var query = ApplyGlobalFilter(set.AsQueryable());
            if (softDeleteEnabled) query = ApplySoftDelete(query);
            if (asNoTracking ?? asNoTrackingDefault) query = query.AsNoTracking();
            if (useSplitQuery ?? splitQueryDefault) query = query.AsSplitQuery();
            if (filter is not null) query = query.Where(filter);
            var defaultIncludes = DefaultIncludes;
            if (defaultIncludes.Length > 0)
            {
                query = ApplyIncludes(query, defaultIncludes);
            }
            if (includeProperties is not null)
            {
                foreach (var includeProperty in includeProperties)
                {
                    if (string.IsNullOrWhiteSpace(includeProperty)) continue;
                    query = query.Include(includeProperty);
                }
            }
            if (includeGraph?.Includes is not null && includeGraph.Includes.Count > 0)
            {
                query = ApplyIncludes(query, includeGraph.Includes);
            }
            if (orderBy is not null) query = orderBy(query);

            var items = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            return items;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("GetAllAsync", filter, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
        bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions)
    {
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("GetAllAsync", filter, cancellationToken).ConfigureAwait(false);
        try
        {
            var query = ApplyGlobalFilter(set.AsQueryable());
            if (softDeleteEnabled) query = ApplySoftDelete(query);
            if (asNoTracking ?? asNoTrackingDefault) query = query.AsNoTracking();
            if (useSplitQuery ?? splitQueryDefault) query = query.AsSplitQuery();
            if (filter is not null) query = query.Where(filter);
            var defaultIncludes = DefaultIncludes;
            if (defaultIncludes.Length > 0)
            {
                query = ApplyIncludes(query, defaultIncludes);
            }
            if (includeExpressions is not null && includeExpressions.Length > 0) query = ApplyIncludes(query, includeExpressions);
            if (orderBy is not null) query = orderBy(query);

            var items = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            return items;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("GetAllAsync", filter, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion

    #region Compiled queries
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<List<TEntity>> GetAllCompiledAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllCompiledAsync(x => true, null, null, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<List<TEntity>> GetAllCompiledAsync(Expression<Func<TEntity, bool>> filter,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default)
    {
        var requestedNoTracking = asNoTracking ?? asNoTrackingDefault;
        var requestedSplit = useSplitQuery ?? splitQueryDefault;
        var useSoftDelete = softDeleteEnabled && !string.IsNullOrWhiteSpace(softDeleteProperty);
        var defaultIncludes = DefaultIncludes;
        var defaultIncludesKey = GetDefaultIncludesKey();

        // Fallback to regular path when global filter or non-trivial filter
        var isTrivialFilter = filter is null
            || (filter.Body is ConstantExpression c && c.Value is bool b && b);
        if (globalQueryFilter is not null || !isTrivialFilter)
        {
            var items = await GetAllAsync(filter, null, asNoTracking, useSplitQuery, cancellationToken).ConfigureAwait(false);
            return [.. items];
        }

        var key = (typeof(TEntity), requestedNoTracking, requestedSplit, useSoftDelete, softDeleteProperty, defaultIncludesKey);
        var del = CompiledGetAll.GetOrAdd(key, _ =>
            BuildCompiledGetAll(requestedNoTracking, requestedSplit, useSoftDelete, softDeleteProperty, defaultIncludes));

        Exception? exception = null;
        var sw = Stopwatch.StartNew();
        await NotifyBeforeAsync("GetAllCompiledAsync", null, cancellationToken).ConfigureAwait(false);
        try
        {
            var asyncQuery = del(db);
            var list = await asyncQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
            if (enableCaching)
            {
                return await cache!.GetOrSetAsync(cacheAllKey,
                    async ct => list,
                    cacheTtl,
                    cancellationToken).ConfigureAwait(false) ?? [];
            }
            return list;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("GetAllCompiledAsync", null, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion

    #region Add / Update / Patch / Remove
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("AddAsync", entity, cancellationToken).ConfigureAwait(false);
        try
        {
            await set.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            await InvalidateCacheAsync(null, cancellationToken).ConfigureAwait(false);
            return entity;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("AddAsync", entity, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var list = entities.ToList();
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("AddRangeAsync", list.Count, cancellationToken).ConfigureAwait(false);
        try
        {
            await set.AddRangeAsync(list, cancellationToken).ConfigureAwait(false);
            await InvalidateCacheAsync(null, cancellationToken).ConfigureAwait(false);
            return list;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("AddRangeAsync", list.Count, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("UpdateAsync", null, cancellationToken).ConfigureAwait(false);
        try
        {
            var keyValues = GetPrimaryKeyValues(entity);
            var existing = await MaterializeByIdAsync(keyValues, false, false, [], cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found for keys {string.Join(',', keyValues)}");
            UpdateEntityProperties(entity, existing);
            await InvalidateCacheAsync(keyValues, cancellationToken).ConfigureAwait(false);
            return existing;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("UpdateAsync", null, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var updated = new List<TEntity>();
        foreach (var entity in entities)
        {
            var u = await UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            updated.Add(u);
        }
        return updated;
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<TEntity?> PatchInternalAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        keyValues ??= [];

        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("PatchAsync", keyValues, cancellationToken).ConfigureAwait(false);
        try
        {
            var entity = await MaterializeByIdAsync(keyValues, false, false, [], cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found for keys {string.Join(',', keyValues)}");

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
            await InvalidateCacheAsync(keyValues, cancellationToken).ConfigureAwait(false);
            return entity;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("PatchAsync", keyValues, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<bool> RemoveAsync(TEntity entity, bool isSoftDelete = true, CancellationToken cancellationToken = default)
    {
        var keyValues = GetPrimaryKeyValues(entity);
        return await RemoveInternalAsync(keyValues, isSoftDelete, cancellationToken).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<bool> RemoveInternalAsync(object?[]? keyValues, bool isSoftDelete = true, CancellationToken cancellationToken = default)
    {
        keyValues ??= [];
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("RemoveAsync", keyValues, cancellationToken).ConfigureAwait(false);
        try
        {
            var entity = await MaterializeByIdAsync(keyValues, false, false, [], cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found for keys {string.Join(',', keyValues)}");

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

            await InvalidateCacheAsync(keyValues, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("RemoveAsync", keyValues, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, bool isSoftDelete = true, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await RemoveAsync(entity, isSoftDelete, cancellationToken).ConfigureAwait(false);
        }
        return true;
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyGlobalFilter(set.AsQueryable());
        if (softDeleteEnabled) query = ApplySoftDelete(query);
        return await query.AnyAsync(filter, cancellationToken).ConfigureAwait(false);
    }
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
        await NotifyBeforeAsync("StreamAsync", filter, cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        var query = ApplyGlobalFilter(set.AsQueryable());
        if (softDeleteEnabled) query = ApplySoftDelete(query);
        if (asNoTracking) query = query.AsNoTracking();
        if (useSplitQuery) query = query.AsSplitQuery();
        if (filter is not null) query = query.Where(filter);
        var defaultIncludes = DefaultIncludes;
        if (defaultIncludes.Length > 0)
        {
            query = ApplyIncludes(query, defaultIncludes);
        }
        if (includeExpressions is not null && includeExpressions.Length > 0) query = ApplyIncludes(query, includeExpressions);
        if (orderBy is not null) query = orderBy(query);

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
            await NotifyAfterAsync("StreamAsync", filter, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<List<TResult>> QueryAsync<TResult>(IKyrolusQuerySpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("QueryAsync.Spec", specification, cancellationToken).ConfigureAwait(false);
        try
        {
            var query = ApplyGlobalFilter(set.AsQueryable());
            if (softDeleteEnabled) query = ApplySoftDelete(query);
            if (specification.AsNoTracking) query = query.AsNoTracking();
            if (specification.Filter is not null) query = query.Where(specification.Filter);
            var defaultIncludes = DefaultIncludes;
            if (defaultIncludes.Length > 0)
            {
                query = ApplyIncludes(query, defaultIncludes);
            }
            if (specification.Includes is not null) query = ApplyIncludes(query, specification.Includes);
            if (specification is IKyrolusHasSplitQuery split && split.UseSplitQuery) query = query.AsSplitQuery();
            if (specification.OrderBy is not null) query = specification.OrderBy(query);
            var result = await query.Select(specification.Selector).ToListAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("QueryAsync.Spec", specification, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<(IReadOnlyList<TResult> Items, int TotalCount)> GetPagedAsync<TResult>(IKyrolusPagedQuerySpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var pageNumber = specification.PageNumber;
        var pageSize = specification.PageSize;
        if (pageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(specification), "PageNumber must be greater than 0.");
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(specification), "PageSize must be greater than 0.");

        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("GetPagedAsync.Spec", (pageNumber, pageSize), cancellationToken).ConfigureAwait(false);
        try
        {
            var query = ApplyGlobalFilter(set.AsQueryable());
            if (softDeleteEnabled) query = ApplySoftDelete(query);
            if (specification.AsNoTracking) query = query.AsNoTracking();
            if (specification.Filter is not null) query = query.Where(specification.Filter);
            var defaultIncludes = DefaultIncludes;
            if (defaultIncludes.Length > 0)
            {
                query = ApplyIncludes(query, defaultIncludes);
            }
            if (specification.Includes is not null) query = ApplyIncludes(query, specification.Includes);
            if (specification is IKyrolusHasSplitQuery split && split.UseSplitQuery) query = query.AsSplitQuery();

            var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            if (specification.OrderBy is not null) query = specification.OrderBy(query);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(specification.Selector)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            return (items, total);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("GetPagedAsync.Spec", (pageNumber, pageSize), exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
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

        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("GetPagedWithDefaultsAsync", (effectivePageNumber, effectivePageSize), cancellationToken).ConfigureAwait(false);
        try
        {
            var query = ApplyGlobalFilter(set.AsQueryable());
            if (softDeleteEnabled) query = ApplySoftDelete(query);
            if (asNoTracking ?? asNoTrackingDefault) query = query.AsNoTracking();
            if (useSplitQuery ?? splitQueryDefault) query = query.AsSplitQuery();
            if (filter is not null) query = query.Where(filter);
            if (specification.Filter is not null) query = query.Where(specification.Filter);
            var defaultIncludes = DefaultIncludes;
            if (defaultIncludes.Length > 0)
            {
                query = ApplyIncludes(query, defaultIncludes);
            }
            if (includeExpressions is not null && includeExpressions.Length > 0) query = ApplyIncludes(query, includeExpressions);
            if (specification.Includes is not null) query = ApplyIncludes(query, specification.Includes);

            var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            if (orderBy is not null) query = orderBy(query);
            if (specification.OrderBy is not null) query = specification.OrderBy(query);

            var items = await query
                .Skip((effectivePageNumber - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            return (items, total);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("GetPagedWithDefaultsAsync", (effectivePageNumber, effectivePageSize), exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
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
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("ExecuteUpdateAsync", filter, cancellationToken).ConfigureAwait(false);
        try
        {
            var effectiveSplit = useSplitQuery ?? splitQueryDefault;
            if (bulkExecutor is not null)
            {
                var count = await bulkExecutor.ExecuteUpdateAsync(filter, setPropertyCalls, effectiveSplit, cancellationToken).ConfigureAwait(false);
                await InvalidateCacheAsync(null, cancellationToken).ConfigureAwait(false);
                return count;
            }

            var query = ApplyGlobalFilter(set.AsQueryable());
            if (softDeleteEnabled) query = ApplySoftDelete(query);
            if (filter is not null) query = query.Where(filter);
            if (effectiveSplit) query = query.AsSplitQuery();
            var result = await query.ExecuteUpdateAsync(setPropertyCalls, cancellationToken).ConfigureAwait(false);
            await InvalidateCacheAsync(null, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("ExecuteUpdateAsync", filter, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public async Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>>? filter = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("ExecuteDeleteAsync", filter, cancellationToken).ConfigureAwait(false);
        try
        {
            var effectiveSplit = useSplitQuery ?? splitQueryDefault;
            if (bulkExecutor is not null)
            {
                var count = await bulkExecutor.ExecuteDeleteAsync(filter, effectiveSplit, cancellationToken).ConfigureAwait(false);
                await InvalidateCacheAsync(null, cancellationToken).ConfigureAwait(false);
                return count;
            }

            var query = ApplyGlobalFilter(set.AsQueryable());
            if (softDeleteEnabled) query = ApplySoftDelete(query);
            if (filter is not null) query = query.Where(filter);
            if (effectiveSplit) query = query.AsSplitQuery();
            var result = await query.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await InvalidateCacheAsync(null, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("ExecuteDeleteAsync", filter, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion

    #region Try* wrappers
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<TEntity>> TryUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            () => UpdateAsync(entity, cancellationToken),
            policy,
            ex => ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionProperty, cancellationToken),
            cancellationToken);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected Task<RepositoryOperationResult<TEntity>> TryPatchInternalAsync(object?[]? keyValues, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        return ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            async () =>
            {
                var entity = await PatchInternalAsync(keyValues, updates, cancellationToken).ConfigureAwait(false);
                return entity!;
            },
            policy,
            ex => ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionProperty, cancellationToken),
            cancellationToken);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    public Task<RepositoryOperationResult<bool>> TryRemoveAsync(TEntity entity, bool isSoftDelete, CancellationToken cancellationToken = default)
    {
        return ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            async () =>
            {
                await RemoveAsync(entity, isSoftDelete, cancellationToken).ConfigureAwait(false);
                return true;
            },
            policy,
            ex => ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionProperty, cancellationToken),
            cancellationToken);
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected Task<RepositoryOperationResult<bool>> TryRemoveInternalAsync(object?[]? keyValues, bool isSoftDelete, CancellationToken cancellationToken = default)
    {
        return ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            async () =>
            {
                await RemoveInternalAsync(keyValues, isSoftDelete, cancellationToken).ConfigureAwait(false);
                return true;
            },
            policy,
            ex => ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionProperty, cancellationToken),
            cancellationToken);
    }
    #endregion

    #region Soft delete helpers
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<bool> RestoreInternalAsync(object?[]? keyValues, CancellationToken cancellationToken)
    {
        keyValues ??= [];
        var sw = Stopwatch.StartNew();
        Exception? exception = null;
        await NotifyBeforeAsync("RestoreAsync", keyValues, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!softDeleteEnabled) throw new InvalidOperationException("Soft delete is not enabled for this repository.");

            var entity = await MaterializeByIdAsync(keyValues, false, false, [], cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found for keys {string.Join(',', keyValues)}");

            var entry = db.Entry(entity);
            var prop = entry.Property(softDeleteProperty);
            prop.CurrentValue = false;
            prop.IsModified = true;

            await InvalidateCacheAsync(keyValues, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            await NotifyAfterAsync("RestoreAsync", keyValues, exception, sw.Elapsed, cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    protected async Task<RepositoryOperationResult<bool>> TryRestoreInternalAsync(object?[]? keyValues, CancellationToken cancellationToken)
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
    }
    #endregion

    #region Helpers
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
        var changedProps = db.Entry(source).Properties
            .Where(p => !Equals(p.CurrentValue, p.OriginalValue));

        var targetEntry = db.Entry(target);
        foreach (var prop in changedProps)
        {
            var targetProp = targetEntry.Property(prop.Metadata.Name);
            targetProp.CurrentValue = prop.CurrentValue;
            targetProp.IsModified = true;
        }
    }

    private async Task InvalidateCacheAsync(object?[]? keyValues, CancellationToken cancellationToken)
    {
        if (!enableCaching || cache is null) return;
        await cache.RemoveAsync(cacheAllKey, cancellationToken).ConfigureAwait(false);
        if (keyValues is { Length: > 0 })
        {
            await cache.RemoveAsync(CacheKeyById(string.Join('|', keyValues.Select(v => v ?? "null"))), cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion
}
