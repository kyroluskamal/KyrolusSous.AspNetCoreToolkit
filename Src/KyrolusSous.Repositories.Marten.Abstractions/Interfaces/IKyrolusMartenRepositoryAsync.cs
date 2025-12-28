
namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

/// <summary>
/// Kyrolus Marten repository contract for single-key documents.
/// Use a bespoke TKey type if you need a custom identity shape.
/// </summary>
public interface IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey>
    where TSession : IDocumentSession
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    IKyrolusMartenObserver? Observer { get; }
    void SetObserver(IKyrolusMartenObserver? observer);
    string? ResolveTenantId(ITenantResolver? resolver);
    IKyrolusMartenAuthorization? Authorization { get; }
    IKyrolusMartenValidation? Validation { get; }
    IKyrolusMartenSoftDeletePolicy? SoftDeletePolicy { get; }
    IKyrolusMartenCacheProvider? CacheProvider { get; }
    IKyrolusMartenResiliencePolicy? ResiliencePolicy { get; }
    IKyrolusMartenTracing? Tracing { get; }

    Task<(TEntity? Entity, Guid? Version)> GetByIdWithVersionAsync(TKey id, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Action<IMartenQueryable<TEntity>>? configureQuery = null,
        string? tenantId = null,
        bool includeSoftDeleted = false,
        CancellationToken cancellationToken = default);

    Task<TEntity?> GetByIdAsync(TKey id, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<IEnumerable<TProjection>> QueryAsync<TProjection>(
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<IEnumerable<TProjection>> QueryAsync<TProjection>(
        IQuerySpecification<TEntity> specification,
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<IEnumerable<TProjection>> QuerySelectAsync<TProjection>(
        Expression<Func<TEntity, bool>>? filter,
        Expression<Func<TEntity, TProjection>> selector,
        Action<IMartenQueryable<TEntity>>? configureQuery = null,
        string? tenantId = null,
        bool includeSoftDeleted = false,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<IEnumerable<TProjection>> QueryWithIncludeAsync<TProjection, TInclude>(
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query,
        Action<TInclude> onInclude,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<IReadOnlyList<TInclude>> QueryWithIncludeToListAsync<TProjection, TInclude>(
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<IDictionary<TKeyInclude, TInclude>> QueryWithIncludeToDictionaryAsync<TProjection, TInclude, TKeyInclude>(
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query,
        Func<TInclude, TKeyInclude> keySelector,
        CancellationToken cancellationToken = default)
        where TProjection : notnull
        where TKeyInclude : notnull;

    Task<PageResult<TProjection>> QueryPageAsync<TProjection>(
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<PageResult<TProjection>> QueryPageAsync<TProjection>(
        IQuerySpecification<TEntity> specification,
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task<TEntity> UpsertAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> UpsertRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<TEntity?> UpdateAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<TEntity?> PatchAsync(TKey id, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<int> PatchWhereAsync(Expression<Func<TEntity, bool>> filter, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(TKey id, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default);

    IAsyncEnumerable<TEntity> StreamAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Action<IMartenQueryable<TEntity>>? configureQuery = null,
        string? tenantId = null,
        bool includeSoftDeleted = false,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<TEntity> StreamBySpecAsync(
        IQuerySpecification<TEntity> specification,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Action<IMartenQueryable<TEntity>>? configureQuery = null,
        string? tenantId = null,
        bool includeSoftDeleted = false,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteCompiledQueryAsync<TCompiled, TResult>(TCompiled query, CancellationToken cancellationToken = default)
        where TCompiled : ICompiledQuery<TEntity, TResult>;

    Task<TResult> WithSessionAsync<TResult>(MartenSessionMode mode, Func<TSession, Task<TResult>> work, CancellationToken cancellationToken = default);

    Task<int> TransformWhereAsync(
        Expression<Func<TEntity, bool>> filter,
        string transformName,
        object? arguments = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

public enum MartenSessionMode
{
    Lightweight,
    IdentityMap,
    DirtyTracking
}
