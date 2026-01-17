namespace KyrolusSous.CQRS.EF.Query;

public sealed class KyrolusEfSeekQuerySpecification<TEntity, TResult>(
    Expression<Func<TEntity, bool>>? filter,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
    IReadOnlyList<Expression<Func<TEntity, object?>>> includes,
    int take,
    bool asNoTracking,
    Expression<Func<TEntity, TResult>> selector,
    bool useSplitQuery)
    : IKyrolusQuerySpecification<TEntity, TResult>, IKyrolusHasSplitQuery, IKyrolusHasLimit
    where TEntity : class
{
    public Expression<Func<TEntity, bool>>? Filter { get; } = filter;
    public Expression<Func<TEntity, TResult>> Selector { get; } = selector;
    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; } = orderBy;
    public IReadOnlyList<Expression<Func<TEntity, object?>>> Includes { get; } = includes ?? [];
    public bool AsNoTracking { get; } = asNoTracking;
    public bool UseSplitQuery { get; } = useSplitQuery;
    public int? Take { get; } = take > 0 ? take : null;
}
