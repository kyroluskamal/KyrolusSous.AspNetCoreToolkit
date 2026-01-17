namespace KyrolusSous.CQRS.EF.Query;

public sealed class KyrolusEfPagedQuerySpecification<TEntity>(
    Expression<Func<TEntity, bool>>? filter,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
    IReadOnlyList<Expression<Func<TEntity, object?>>> includes,
    int pageNumber,
    int pageSize,
    bool asNoTracking,
    Expression<Func<TEntity, TEntity>>? selector = null)
    : IKyrolusPagedQuerySpecification<TEntity, TEntity>
    where TEntity : class
{
    public Expression<Func<TEntity, bool>>? Filter { get; } = filter;
    public Expression<Func<TEntity, TEntity>> Selector { get; } = selector ?? (static entity => entity);
    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; } = orderBy;
    public IReadOnlyList<Expression<Func<TEntity, object?>>> Includes { get; } = includes ?? [];
    public bool AsNoTracking { get; } = asNoTracking;
    public int PageNumber { get; } = pageNumber;
    public int PageSize { get; } = pageSize;
}
