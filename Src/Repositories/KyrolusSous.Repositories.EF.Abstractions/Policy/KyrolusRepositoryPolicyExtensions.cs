using KyrolusSous.Caching.Abstractions;

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
    /// Convenience overload: register a predicate; internally becomes query => query.Where(predicate).
    /// </summary>
    public static KyrolusRepositoryPolicy AddGlobalWhereFilter<TEntity>(
        this KyrolusRepositoryPolicy policy,
        Expression<Func<TEntity, bool>> predicate)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return AddGlobalQueryFilter<TEntity>(policy, q => q.Where(predicate));
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
            IQueryable<TEntity> q = query;

            foreach (var d in typed)
            {
                if (d is Func<IQueryable<TEntity>, IQueryable<TEntity>> f)
                    q = f(q);
                else
                    throw new InvalidOperationException(
                        $"GlobalQueryFilters for '{typeof(TEntity).Name}' contains an invalid delegate type: '{d.GetType().Name}'.");
            }

            return q;
        };
    }

    /// <summary>
    /// Sets a cache policy for a specific entity type.
    /// </summary>
    public static KyrolusRepositoryPolicy SetCachePolicy<TEntity>(
        this KyrolusRepositoryPolicy policy,
        KyrolusCachePolicy cachePolicy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(cachePolicy);
        policy.CachePolicies[typeof(TEntity)] = cachePolicy;
        return policy;
    }

    /// <summary>
    /// Returns the cache policy for a specific entity type, or the default cache policy if configured.
    /// </summary>
    public static KyrolusCachePolicy? GetCachePolicy<TEntity>(
        this KyrolusRepositoryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.CachePolicies.TryGetValue(typeof(TEntity), out var cachePolicy))
            return cachePolicy;
        return policy.DefaultCachePolicy;
    }

    public static KyrolusRepositoryPolicy SetCacheReadOperations<TEntity>(
    this KyrolusRepositoryPolicy policy,
    KyrolusCacheReadOperations operations)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.CacheReadOperations[typeof(TEntity)] = operations;
        return policy;
    }

    public static KyrolusCacheReadOperations GetCacheReadOperations<TEntity>(
        this KyrolusRepositoryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return policy.CacheReadOperations.TryGetValue(typeof(TEntity), out var ops)
            ? ops
            : policy.DefaultCacheReadOperations;
    }

    public static KyrolusRepositoryPolicy SetDefaultIncludeProperties<TEntity>(
        this KyrolusRepositoryPolicy policy,
        params string[] includeProperties)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (includeProperties is null)
        {
            policy.DefaultIncludeProperties.Remove(typeof(TEntity));
            return policy;
        }

        var normalized = NormalizeIncludeProperties(includeProperties);
        if (normalized.Length == 0)
            policy.DefaultIncludeProperties.Remove(typeof(TEntity));
        else
            policy.DefaultIncludeProperties[typeof(TEntity)] = normalized;

        return policy;
    }

    public static string[] GetDefaultIncludeProperties<TEntity>(
        this KyrolusRepositoryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.DefaultIncludeProperties.TryGetValue(typeof(TEntity), out var includeProperties) || includeProperties is null)
            return [];

        return NormalizeIncludeProperties(includeProperties);
    }

    private static string[] NormalizeIncludeProperties(IEnumerable<string> includeProperties)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var include in includeProperties)
        {
            if (string.IsNullOrWhiteSpace(include)) continue;
            var trimmed = include.Trim();
            if (trimmed.Length == 0) continue;
            if (seen.Add(trimmed))
                result.Add(trimmed);
        }

        return [.. result];
    }

}
