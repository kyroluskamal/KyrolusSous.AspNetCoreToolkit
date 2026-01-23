namespace KyrolusSous.CQRS.EF.Query;

public sealed class KyrolusEfPagedQuerySpecification<TEntity>(
    SpecificationInputs<TEntity, TEntity> spec,
    int pageNumber,
    int pageSize)
    : IKyrolusPagedQuerySpecification<TEntity, TEntity>
    where TEntity : class
{
    public int PageNumber { get; } = pageNumber;
    public int PageSize { get; } = pageSize;
    public Expression<Func<TEntity, bool>>? Filter { get; } = spec.Filter;
    public Expression<Func<TEntity, TEntity>>? Selector { get; } = spec.Selector;
    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; } = spec.OrderBy;
    public bool AsNoTracking { get; } = spec.AsNoTracking;
    public bool UseSplitQuery { get; } = spec.UseSplitQuery;
    public bool IncludeDeleted => spec.IncludeDeleted;

    public Expression<Func<TEntity, object?>>[]? Includes => spec.Includes;
}
