namespace KyrolusSous.Repositories.EF.Abstractions.Policy;

public static class KyrolusRepositoryPolicyExtensions
{
    /// <summary>
    /// Adds a global query filter for a specific entity type. You can add multiple filters per entity.
    /// </summary>
    public static KyrolusRepositoryPolicy AddGlobalQueryFilter<TEntity>(
        this KyrolusRepositoryPolicy policy,
        Func<IQueryable<TEntity>, IQueryable<TEntity>> filter)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(filter);
        var key = typeof(TEntity);
        if (!policy.GlobalQueryFilters.TryGetValue(key, out var list))
        {
            list = [];
            policy.GlobalQueryFilters[key] = list;
        }
        list.Add(filter);
        return policy;
    }

    /// <summary>
    /// Returns a single composed filter (pipeline) for the entity type, or null if none exist.
    /// </summary>
    public static Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetGlobalQueryFilter<TEntity>(
        this KyrolusRepositoryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.GlobalQueryFilters.TryGetValue(typeof(TEntity), out var list) || list.Count == 0)
            return null;
        var typed = list.Cast<Func<IQueryable<TEntity>, IQueryable<TEntity>>>().ToArray();
        return query =>
        {
            foreach (var f in typed)
                query = f(query);
            return query;
        };
    }
}