global using System.Linq.Expressions;

namespace KyrolusSous.IRespositoryInterfaces.IRepository;

public interface IRepositoryAsync<TDbcontext, TEntity, TKey>
where TEntity : class
where TKey : IEquatable<TKey>
where TDbcontext : class
{
    Task<TEntity?> GetByIdAsync(TKey id,
        List<string>? includeProperties = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
                            List<string>? includeProperties = null, CancellationToken cancellationToken = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    Task<TEntity?> UpdateAsync(TEntity entity);
    Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities);
    Task<TEntity?> PatchAsync(object?[]? keyValues, Dictionary<string, object> updates);
    Task<bool> Remove(TEntity entity);
    Task<bool> Remove(object?[]? keyValues);
    Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities);
    Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default);
}
