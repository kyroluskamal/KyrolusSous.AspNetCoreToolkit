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
        var prop = typeof(TEntity).GetProperty("IsDeleted", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null || prop.PropertyType != typeof(bool))
        {
            enabled = false;
            isDeletedPropertyName = string.Empty;
        }
        else
        {
            enabled = services?.SoftDeletePolicy?.Enabled ?? true;
            filterByDefault = services?.SoftDeletePolicy?.FilterDeletedByDefault ?? true;
            isDeletedPropertyName = prop.Name;
        }
    }

    private bool ShouldFilter(bool includeSoftDeleted) => enabled && !includeSoftDeleted && filterByDefault && !string.IsNullOrEmpty(isDeletedPropertyName);

    public override async Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Action<IMartenQueryable<TEntity>>? configureQuery = null,
        string? tenantId = null,
        bool includeSoftDeleted = false,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFilter(includeSoftDeleted))
        {
            var param = Expression.Parameter(typeof(TEntity), "e");
            var prop = Expression.Property(param, isDeletedPropertyName);
            var notDeleted = Expression.Equal(prop, Expression.Constant(false));
            var lambda = Expression.Lambda<Func<TEntity, bool>>(notDeleted, param);
            filter = filter is null ? lambda : Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(filter.Body, Expression.Invoke(lambda, filter.Parameters)), filter.Parameters);
        }
        return await base.GetAllAsync(filter, orderBy, configureQuery, tenantId, includeSoftDeleted, cancellationToken).ConfigureAwait(false);
    }

    public override async IAsyncEnumerable<TEntity> StreamAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Action<IMartenQueryable<TEntity>>? configureQuery = null,
        string? tenantId = null,
        bool includeSoftDeleted = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in await GetAllAsync(filter, orderBy, configureQuery, tenantId, includeSoftDeleted, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
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

        var entity = await base.GetByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return false;
        ApplyProperty(entity, isDeletedPropertyName, true);
        await base.UpdateAsync(entity, expectedVersion, tenantId, cancellationToken).ConfigureAwait(false);
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
        var entity = await Session.LoadAsync<TEntity>(id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return false;
        ApplyProperty(entity, isDeletedPropertyName, false);
        Session.Store(entity);
        return true;
    }
}
