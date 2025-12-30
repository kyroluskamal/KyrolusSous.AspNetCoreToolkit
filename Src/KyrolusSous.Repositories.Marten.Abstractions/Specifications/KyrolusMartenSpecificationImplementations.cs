using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions.Specifications;

public sealed class KyrolusMartenDelegateSpecification<TEntity>(Func<IMartenQueryable<TEntity>, IMartenQueryable<TEntity>> apply) : IQuerySpecification<TEntity>
{
    private readonly Func<IMartenQueryable<TEntity>, IMartenQueryable<TEntity>> apply = apply ?? throw new ArgumentNullException(nameof(apply));

    public IMartenQueryable<TEntity> Apply(IMartenQueryable<TEntity> queryable)
        => apply(queryable);
}

public sealed class KyrolusMartenFilterSpecification<TEntity>(Expression<Func<TEntity, bool>> filter) : IQuerySpecification<TEntity>
{
    private readonly Expression<Func<TEntity, bool>> filter = filter ?? throw new ArgumentNullException(nameof(filter));

    public IMartenQueryable<TEntity> Apply(IMartenQueryable<TEntity> queryable)
        => (IMartenQueryable<TEntity>)queryable.Where(filter);
}

public sealed class KyrolusMartenOrderSpecification<TEntity>(Func<IMartenQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy) : IQuerySpecification<TEntity>
{
    private readonly Func<IMartenQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = orderBy ?? throw new ArgumentNullException(nameof(orderBy));

    public IMartenQueryable<TEntity> Apply(IMartenQueryable<TEntity> queryable)
        => (IMartenQueryable<TEntity>)orderBy(queryable);
}

public sealed class KyrolusMartenPaginationSpecification<TEntity> : IQuerySpecification<TEntity>
{
    private readonly int skip;
    private readonly int take;

    public KyrolusMartenPaginationSpecification(int skip, int take)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));
        this.skip = skip;
        this.take = take;
    }

    public IMartenQueryable<TEntity> Apply(IMartenQueryable<TEntity> queryable)
        => (IMartenQueryable<TEntity>)queryable.Skip(skip).Take(take);
}

public sealed class KyrolusMartenIncludeSpecification<TEntity>(Action<IMartenQueryable<TEntity>> include) : IQuerySpecification<TEntity>
{
    private readonly Action<IMartenQueryable<TEntity>> include = include ?? throw new ArgumentNullException(nameof(include));

    public IMartenQueryable<TEntity> Apply(IMartenQueryable<TEntity> queryable)
    {
        include(queryable);
        return queryable;
    }
}

public sealed class KyrolusMartenCompositeSpecification<TEntity>(IEnumerable<IQuerySpecification<TEntity>> specifications) : IQuerySpecification<TEntity>
{
    private readonly IQuerySpecification<TEntity>[] specifications = specifications?.ToArray() ?? throw new ArgumentNullException(nameof(specifications));

    public IMartenQueryable<TEntity> Apply(IMartenQueryable<TEntity> queryable)
    {
        foreach (var spec in specifications)
        {
            queryable = spec.Apply(queryable);
        }
        return queryable;
    }
}
