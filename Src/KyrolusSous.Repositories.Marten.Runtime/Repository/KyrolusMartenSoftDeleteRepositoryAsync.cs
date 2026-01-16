namespace KyrolusSous.Repositories.Marten.Runtime.Repository;

public class KyrolusMartenSoftDeleteRepositoryAsync<TSession, TEntity, TKey>
    : KyrolusMartenRepositoryAsync<TSession, TEntity, TKey>, IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TEntity, TKey>
    where TSession : IDocumentSession
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    private readonly string isDeletedPropertyName;
    private readonly bool filterByDefault;
    private readonly bool enabled;
    private readonly PropertyInfo? isDeletedProperty;

    public KyrolusMartenSoftDeleteRepositoryAsync(
        TSession session,
        KyrolusMartenRepositoryDependencies? services = null)
        : base(session, services)
    {
        var configuredName = services?.SoftDeletePolicy?.PropertyName?.Trim();
        if (string.IsNullOrWhiteSpace(configuredName))
        {
            enabled = false;
            isDeletedPropertyName = string.Empty;
            isDeletedProperty = null;
        }
        else
        {
            var prop = typeof(TEntity).GetProperty(configuredName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || prop.PropertyType != typeof(bool))
            {
                enabled = false;
                isDeletedPropertyName = string.Empty;
                isDeletedProperty = null;
            }
            else
            {
                enabled = services?.SoftDeletePolicy?.Enabled ?? true;
                filterByDefault = services?.SoftDeletePolicy?.FilterDeletedByDefault ?? true;
                isDeletedPropertyName = prop.Name; // preserve actual casing
                isDeletedProperty = prop;
            }
        }
    }

    private bool ShouldFilter(bool includeSoftDeleted) => enabled && !includeSoftDeleted && filterByDefault && !string.IsNullOrEmpty(isDeletedPropertyName);
    private bool ShouldFilterById() => enabled && filterByDefault && !string.IsNullOrEmpty(isDeletedPropertyName);
    private bool IsDeleted(TEntity entity) => isDeletedProperty?.GetValue(entity) is true;

    public override async Task<IEnumerable<TEntity>> GetAllAsync(
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        if (ShouldFilter(opts.IncludeSoftDeleted))
        {
            var param = Expression.Parameter(typeof(TEntity), "e");
            var prop = Expression.Property(param, isDeletedPropertyName);
            var notDeleted = Expression.Equal(prop, Expression.Constant(false));
            var lambda = Expression.Lambda<Func<TEntity, bool>>(notDeleted, param);
            var combined = opts.Filter is null ? lambda : Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(opts.Filter.Body, Expression.Invoke(lambda, opts.Filter.Parameters)), opts.Filter.Parameters);
            opts = opts with { Filter = combined };
        }
        return await base.GetAllAsync(opts, cancellationToken).ConfigureAwait(false);
    }

    public override async Task<MartenEntityResult<TEntity>?> GetByIdAsync(
        TKey id,
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var session = ResolveSession(opts);
        var hasIncludes = (opts.IncludeProperties?.Count ?? 0) > 0 || (opts.IncludeExpressions?.Length ?? 0) > 0;
        var shouldFilter = ShouldFilterById();

        if (CacheProvider is not null && !hasIncludes)
        {
            var cachePolicy = await ResolveCachePolicyAsync("GetByIdAsync", opts.TenantId, cancellationToken).ConfigureAwait(false);
            if (IsCacheEnabled(cachePolicy))
            {
                var cacheKey = BuildCacheKey(opts.TenantId, id, cachePolicy.KeySuffix);
                var cacheOptions = BuildCacheEntryOptions(cachePolicy, opts.TenantId);
                return await CacheProvider.GetOrCreateAsync<MartenEntityResult<TEntity>?>(cacheKey,
                    async ct =>
                    {
                        var entity = await session.LoadAsync<TEntity>(id, ct).ConfigureAwait(false);
                        if (entity is null) return null;
                        if (shouldFilter && IsDeleted(entity)) return null;
                        await ApplyIncludesAsync(entity, opts.IncludeProperties, opts.IncludeExpressions, session, ct).ConfigureAwait(false);
                        var metadata = await session.MetadataForAsync(entity, ct).ConfigureAwait(false);
                        var version = ReadVersion(metadata);
                        return new MartenEntityResult<TEntity>(entity, version);
                    },
                    cacheOptions,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var item = await session.LoadAsync<TEntity>(id, cancellationToken).ConfigureAwait(false);
        if (item is null) return null;
        if (shouldFilter && IsDeleted(item)) return null;
        await ApplyIncludesAsync(item, opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
        var meta = await session.MetadataForAsync(item, cancellationToken).ConfigureAwait(false);
        var ver = ReadVersion(meta);
        return new MartenEntityResult<TEntity>(item, ver);
    }

    public async Task<MartenEntityResult<TEntity>?> GetByIdIncludingDeletedAsync(
        TKey id,
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        var session = ResolveSession(opts);
        var hasIncludes = (opts.IncludeProperties?.Count ?? 0) > 0 || (opts.IncludeExpressions?.Length ?? 0) > 0;

        if (CacheProvider is not null && !hasIncludes)
        {
            var cachePolicy = await ResolveCachePolicyAsync("GetByIdIncludingDeletedAsync", opts.TenantId, cancellationToken).ConfigureAwait(false);
            if (IsCacheEnabled(cachePolicy))
            {
                var cacheKey = BuildCacheKey(opts.TenantId, id, cachePolicy.KeySuffix) + ":incdel=1";
                var cacheOptions = BuildCacheEntryOptions(cachePolicy, opts.TenantId);
                return await CacheProvider.GetOrCreateAsync<MartenEntityResult<TEntity>?>(cacheKey,
                    async ct =>
                    {
                        var entity = await session.LoadAsync<TEntity>(id, ct).ConfigureAwait(false);
                        if (entity is null) return null;
                        await ApplyIncludesAsync(entity, opts.IncludeProperties, opts.IncludeExpressions, session, ct).ConfigureAwait(false);
                        var metadata = await session.MetadataForAsync(entity, ct).ConfigureAwait(false);
                        var version = ReadVersion(metadata);
                        return new MartenEntityResult<TEntity>(entity, version);
                    },
                    cacheOptions,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var item = await session.LoadAsync<TEntity>(id, cancellationToken).ConfigureAwait(false);
        if (item is null) return null;
        await ApplyIncludesAsync(item, opts.IncludeProperties, opts.IncludeExpressions, session, cancellationToken).ConfigureAwait(false);
        var meta = await session.MetadataForAsync(item, cancellationToken).ConfigureAwait(false);
        var ver = ReadVersion(meta);
        return new MartenEntityResult<TEntity>(item, ver);
    }

    public Task<IEnumerable<TEntity>> GetAllIncludingDeletedAsync(
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        opts = opts with { IncludeSoftDeleted = true };
        return base.GetAllAsync(opts, cancellationToken);
    }

    public async Task<IEnumerable<TEntity>> GetDeletedOnlyAsync(
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!enabled || string.IsNullOrEmpty(isDeletedPropertyName))
        {
            return Array.Empty<TEntity>();
        }

        var opts = options ?? new MartenQueryOptions<TEntity>();
        var param = Expression.Parameter(typeof(TEntity), "e");
        var prop = Expression.Property(param, isDeletedPropertyName);
        var isDeleted = Expression.Equal(prop, Expression.Constant(true));
        var deletedFilter = Expression.Lambda<Func<TEntity, bool>>(isDeleted, param);
        if (opts.Filter is not null)
        {
            deletedFilter = Expression.Lambda<Func<TEntity, bool>>(
                Expression.AndAlso(opts.Filter.Body, Expression.Invoke(deletedFilter, opts.Filter.Parameters)),
                opts.Filter.Parameters);
        }

        opts = opts with { Filter = deletedFilter, IncludeSoftDeleted = true };
        return await base.GetAllAsync(opts, cancellationToken).ConfigureAwait(false);
    }

    protected override IEnumerable<string> GetAdditionalEntityCacheKeysForInvalidation(
        TKey id,
        string? tenantId,
        KyrolusCachePolicy policy)
    {
        if (!enabled || string.IsNullOrEmpty(isDeletedPropertyName))
        {
            return Array.Empty<string>();
        }

        return [BuildCacheKey(tenantId, id, policy.KeySuffix) + ":incdel=1"];
    }

    public override async IAsyncEnumerable<TEntity> StreamAsync(
        MartenQueryOptions<TEntity>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in await GetAllAsync(options, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    public override async Task<PageResult<TEntity>> GetPageAsync(MartenQueryOptions<TEntity>? options = null, MartenPageRequest? page = null, CancellationToken cancellationToken = default)
    {
        var opts = options ?? new MartenQueryOptions<TEntity>();
        if (ShouldFilter(opts.IncludeSoftDeleted))
        {
            var param = Expression.Parameter(typeof(TEntity), "e");
            var prop = Expression.Property(param, isDeletedPropertyName);
            var notDeleted = Expression.Equal(prop, Expression.Constant(false));
            var lambda = Expression.Lambda<Func<TEntity, bool>>(notDeleted, param);
            var combined = opts.Filter is null ? lambda : Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(opts.Filter.Body, Expression.Invoke(lambda, opts.Filter.Parameters)), opts.Filter.Parameters);
            opts = opts with { Filter = combined };
        }
        return await base.GetPageAsync(opts, page, cancellationToken).ConfigureAwait(false);
    }

    public override Task<bool> RemoveAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (!enabled || string.IsNullOrEmpty(isDeletedPropertyName))
        {
            return base.RemoveAsync(entity, expectedVersion, tenantId, cancellationToken);
        }

        ApplyProperty(entity, isDeletedPropertyName, true);
        return base.UpdateAsync(entity, expectedVersion, tenantId, cancellationToken).ContinueWith(_ => true, cancellationToken);
    }

    public override async Task<bool> RemoveAsync(TKey id, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (!enabled || string.IsNullOrEmpty(isDeletedPropertyName))
        {
            return await base.RemoveAsync(id, expectedVersion, tenantId, cancellationToken).ConfigureAwait(false);
        }

        var result = await base.GetByIdAsync(id, new MartenQueryOptions<TEntity>(TenantId: tenantId), cancellationToken).ConfigureAwait(false);
        if (result?.Entity is null) return false;
        ApplyProperty(result.Entity, isDeletedPropertyName, true);
        await base.UpdateAsync(result.Entity, expectedVersion, tenantId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public override Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (!enabled || string.IsNullOrEmpty(isDeletedPropertyName))
        {
            return base.DeleteWhereAsync(filter, tenantId, cancellationToken);
        }

        var patch = Session.Patch<TEntity>(filter);
        patch.Set(isDeletedPropertyName, true);
        return Task.FromResult(0);
    }

    public Task<bool> RestoreAsync(TKey id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (!enabled || string.IsNullOrEmpty(isDeletedPropertyName)) return Task.FromResult(false);
        return RestoreInternalAsync(id, cancellationToken);
    }

    public async Task<bool> RestoreRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (!enabled || string.IsNullOrEmpty(isDeletedPropertyName)) return false;
        foreach (var entity in entities)
        {
            ApplyProperty(entity, isDeletedPropertyName, false);
            Session.Store(entity);
        }
        return true;
    }

    public Task<int> RestoreWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (!enabled || string.IsNullOrEmpty(isDeletedPropertyName)) return Task.FromResult(0);
        var patch = Session.Patch<TEntity>(filter);
        patch.Set(isDeletedPropertyName, false);
        return Task.FromResult(0);
    }

    private async Task<bool> RestoreInternalAsync(TKey id, CancellationToken cancellationToken)
    {
        var result = await base.GetByIdAsync(id, null, cancellationToken).ConfigureAwait(false);
        if (result?.Entity is null) return false;
        ApplyProperty(result.Entity, isDeletedPropertyName, false);
        Session.Store(result.Entity);
        return true;
    }
}
