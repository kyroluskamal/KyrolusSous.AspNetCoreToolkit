namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public sealed class IncludeGraph<TEntity>(params Expression<Func<TEntity, object?>>[] includes)
{
    public IReadOnlyList<Expression<Func<TEntity, object?>>> Includes { get; } = includes ?? [];
}
