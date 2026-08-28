using Marten.Linq;

namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusQuerySpecification<TEntity>
{
    IMartenQueryable<TEntity> Apply(IMartenQueryable<TEntity> queryable);
}
