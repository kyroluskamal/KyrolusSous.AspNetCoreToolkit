using System.Linq.Expressions;

namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

/// <summary>
/// Soft-delete aware Marten repository contract.
/// </summary>
public interface IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TEntity, TKey> : IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey>
    where TSession : IDocumentSession
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    Task<IEnumerable<TEntity>> GetAllIncludingDeletedAsync(
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<TEntity>> GetDeletedOnlyAsync(
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default);

    Task<MartenEntityResult<TEntity>?> GetByIdIncludingDeletedAsync(
        TKey id,
        MartenQueryOptions<TEntity>? options = null,
        CancellationToken cancellationToken = default);

    Task<bool> RestoreAsync(TKey id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> RestoreRangeAsync(IEnumerable<TEntity> entities, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<int> RestoreWhereAsync(Expression<Func<TEntity, bool>> filter, string? tenantId = null, CancellationToken cancellationToken = default);
}
