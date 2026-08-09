using Marten.Linq;

namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IQuerySpecification<TEntity>
{
    IMartenQueryable<TEntity> Apply(IMartenQueryable<TEntity> queryable);
}
