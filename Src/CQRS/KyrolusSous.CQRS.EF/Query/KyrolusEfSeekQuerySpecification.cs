namespace KyrolusSous.CQRS.EF.Query;

public record SpecificationInputs<TEntity, TResult>(Expression<Func<TEntity, bool>>? Filter,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy,
    bool AsNoTracking,
    bool UseSplitQuery,
    bool IncludeDeleted,
    Expression<Func<TEntity, TResult>>? Selector,
    Expression<Func<TEntity, object?>>[]? Includes);
public sealed class KyrolusEfSeekQuerySpecification<TEntity, TResult>(
        SpecificationInputs<TEntity, TResult> spec
    )
    : IKyrolusQuerySpecification<TEntity, TResult>, IKyrolusHasSplitQuery
    where TEntity : class
{
    public Expression<Func<TEntity, bool>>? Filter { get; } = spec.Filter;
    public Expression<Func<TEntity, TResult>>? Selector { get; } = spec.Selector;
    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; } = spec.OrderBy;
    public bool AsNoTracking { get; } = spec.AsNoTracking;
    public bool UseSplitQuery { get; } = spec.UseSplitQuery;
    public bool IncludeDeleted => spec.IncludeDeleted;
    public Expression<Func<TEntity, object?>>[]? Includes => spec.Includes;

}
