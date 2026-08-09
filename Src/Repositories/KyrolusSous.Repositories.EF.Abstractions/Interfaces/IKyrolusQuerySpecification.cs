namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusQuerySpecification<TEntity, TResult>
{
    Expression<Func<TEntity, bool>>? Filter { get; }
    Expression<Func<TEntity, TResult>>? Selector { get; }
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; }
    Expression<Func<TEntity, object?>>[]? Includes { get; }
    bool AsNoTracking { get; }
    bool IncludeDeleted { get; }
}
