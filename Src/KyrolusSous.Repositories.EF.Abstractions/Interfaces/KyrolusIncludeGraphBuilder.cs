namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public static class KyrolusIncludeGraphBuilder
{
    public static IncludeGraph<TEntity> FromPaths<TEntity>(params string[] paths)
    {
        var includes = new List<Expression<Func<TEntity, object?>>>();
        if (paths == null) return new IncludeGraph<TEntity>(Array.Empty<Expression<Func<TEntity, object?>>>());
        foreach (var path in paths)
        {
            var expr = KyrolusEFRepositoryBase<TEntity>.BuildIncludeExpression(path);
            if (expr is not null) includes.Add(expr);
        }
        return new IncludeGraph<TEntity>([.. includes]);
    }
}
