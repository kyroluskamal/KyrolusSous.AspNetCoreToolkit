namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusSoftDeleteRepository<TEntity>
    where TEntity : class
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        List<string>? includeProperties = null,
        IncludeGraph<TEntity>? includeGraph = null,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IReadOnlyList<TEntity>> GetAllIncludingDeletedAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default, params Expression<Func<TEntity, object?>>[] includeExpressions);
}
