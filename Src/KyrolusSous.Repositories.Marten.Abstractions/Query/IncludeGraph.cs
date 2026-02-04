using System.Linq.Expressions;

namespace KyrolusSous.Repositories.Marten.Abstractions.Query;

public sealed class IncludeGraph<TEntity>
{
    public List<Expression<Func<TEntity, object?>>> Includes { get; } = [];

    public IncludeGraph(params Expression<Func<TEntity, object?>>[] includes)
    {
        if (includes is { Length: > 0 })
        {
            Includes.AddRange(includes);
        }
    }
}
