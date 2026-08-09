using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF.Query;

internal static class KyrolusIncludeMerge
{
    public static Expression<Func<TEntity, object?>>[]? MergeExpressions<TEntity>(
        List<string>? includeProperties,
        IncludeGraph<TEntity>? includeGraph,
        Expression<Func<TEntity, object?>>[]? includeExpressions)
        where TEntity : class
    {
        var list = new List<Expression<Func<TEntity, object?>>>();
        var converted = KyrolusEFRepositoryBase<TEntity>.ConvertIncludePropertiesToExpressions(includeProperties);
        if (converted is { Length: > 0 }) list.AddRange(converted);
        if (includeGraph?.Includes is { Count: > 0 }) list.AddRange(includeGraph.Includes);
        if (includeExpressions is { Length: > 0 }) list.AddRange(includeExpressions);
        return list.Count == 0 ? null : list.ToArray();
    }

    public static Expression<Func<TEntity, object?>>[]? MergeExpressions<TEntity>(
        Expression<Func<TEntity, object?>>[]? includeExpressions,
        IncludeGraph<TEntity>? includeGraph)
        where TEntity : class
    {
        if (includeGraph?.Includes is not { Count: > 0 }) return includeExpressions;
        if (includeExpressions is null || includeExpressions.Length == 0)
        {
            return includeGraph.Includes.ToArray();
        }

        var list = new List<Expression<Func<TEntity, object?>>>(includeGraph.Includes.Count + includeExpressions.Length);
        list.AddRange(includeGraph.Includes);
        list.AddRange(includeExpressions);
        return list.ToArray();
    }

    public static IncludeGraph<TEntity>? MergeGraph<TEntity>(
        IncludeGraph<TEntity>? includeGraph,
        Expression<Func<TEntity, object?>>[]? includeExpressions)
        where TEntity : class
    {
        if (includeExpressions is null || includeExpressions.Length == 0) return includeGraph;
        var list = new List<Expression<Func<TEntity, object?>>>(includeExpressions.Length);
        if (includeGraph?.Includes is { Count: > 0 }) list.AddRange(includeGraph.Includes);
        list.AddRange(includeExpressions);
        return new IncludeGraph<TEntity>(list.ToArray());
    }
}
