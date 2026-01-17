namespace KyrolusSous.CQRS.EF.Query;

public sealed class KyrolusEfQuerySpecification<TEntity, TResult>(
    Expression<Func<TEntity, bool>>? filter,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
    IReadOnlyList<Expression<Func<TEntity, object?>>> includes,
    Expression<Func<TEntity, TResult>> selector,
    bool asNoTracking)
    : IKyrolusQuerySpecification<TEntity, TResult>
    where TEntity : class
{
    public Expression<Func<TEntity, bool>>? Filter { get; } = filter;
    public Expression<Func<TEntity, TResult>> Selector { get; } = selector;
    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; } = orderBy;
    public IReadOnlyList<Expression<Func<TEntity, object?>>> Includes { get; } = includes ?? [];
    public bool AsNoTracking { get; } = asNoTracking;
}
