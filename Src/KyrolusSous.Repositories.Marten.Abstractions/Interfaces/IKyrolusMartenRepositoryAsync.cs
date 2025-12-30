
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

    Task<IEnumerable<TEntity>> GetAllAsync(
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default);

    Task<MartenEntityResult<TEntity>?> GetByIdAsync(
        TKey id,
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<TProjection>> QueryAsync<TProjection>(
        MartenQueryOptions<TEntity>? options,
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<PageResult<TProjection>> QueryPageAsync<TProjection>(
        MartenQueryOptions<TEntity>? options,
        Func<IMartenQueryable<TEntity>, IMartenQueryable<TProjection>> selector,
        MartenPageRequest? page = null,
        CancellationToken cancellationToken = default) where TProjection : notnull;

    Task<PageResult<TEntity>> GetPageAsync(
        MartenQueryOptions<TEntity>? options = null,
        MartenPageRequest? page = null,
        CancellationToken cancellationToken = default);

    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task<TEntity> UpsertAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> UpsertRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<TEntity?> UpdateAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<MartenEntityResult<TEntity>?> PatchAsync(TKey id, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<int> PatchWhereAsync(Expression<Func<TEntity, bool>> filter, Dictionary<string, object> updates, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(TEntity entity, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(TKey id, Guid? expectedVersion = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default);

    IAsyncEnumerable<TEntity> StreamAsync(
        MartenQueryOptions<TEntity>? options = null,
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
