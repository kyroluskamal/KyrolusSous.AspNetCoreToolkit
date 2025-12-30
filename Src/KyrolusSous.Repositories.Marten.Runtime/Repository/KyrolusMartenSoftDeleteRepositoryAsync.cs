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
        }
        else
        {
            var prop = typeof(TEntity).GetProperty(configuredName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || prop.PropertyType != typeof(bool))
            {
                enabled = false;
                isDeletedPropertyName = string.Empty;
            }
            else
            {
                enabled = services?.SoftDeletePolicy?.Enabled ?? true;
                filterByDefault = services?.SoftDeletePolicy?.FilterDeletedByDefault ?? true;
                isDeletedPropertyName = prop.Name; // preserve actual casing
            }
        }
    }

    private bool ShouldFilter(bool includeSoftDeleted) => enabled && !includeSoftDeleted && filterByDefault && !string.IsNullOrEmpty(isDeletedPropertyName);

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
