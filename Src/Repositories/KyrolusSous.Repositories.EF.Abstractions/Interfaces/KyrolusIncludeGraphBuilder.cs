namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public static class KyrolusIncludeGraphBuilder
{
    public static IncludeGraph<TEntity> FromPaths<TEntity>(params string[]? paths)
    {
        var includes = new List<Expression<Func<TEntity, object?>>>();
        if (paths is null || paths.Length == 0)
            return new IncludeGraph<TEntity>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                var expr = KyrolusEFRepositoryBase<TEntity>.BuildIncludeExpression(path);
                if (expr is not null) includes.Add(expr);
            }
            catch (ArgumentException)
            {
                // Ignore invalid include paths gracefully in builder
            }
        }
        return new IncludeGraph<TEntity>([.. includes]);
    }
}
