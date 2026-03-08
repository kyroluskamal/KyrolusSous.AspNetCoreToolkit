namespace KyrolusSous.Repositories.Marten.Abstractions.Query;

public static class KyrolusIncludeGraphBuilder
{
    public static IncludeGraph<TEntity> FromPaths<TEntity>(params string[]? paths)
    {
        var includes = new List<Expression<Func<TEntity, object?>>>();
        if (paths is null || paths.Length == 0)
        {
            return new IncludeGraph<TEntity>();
        }

        foreach (var path in paths)
        {
            var expression = KyrolusQueryExpressionBuilder<TEntity>.BuildIncludeExpression(path);
            if (expression is not null)
            {
                includes.Add(expression);
            }
        }

        return new IncludeGraph<TEntity>([.. includes]);
    }
}
