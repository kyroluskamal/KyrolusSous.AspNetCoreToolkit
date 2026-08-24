namespace KyrolusSous.Mapping.Runtime.Projections;

/// <summary>
/// Provides LINQ projection extensions on <see cref="IQueryable"/> for efficient database-level query optimization (e.g. EF Core SQL <c>SELECT</c> projection).
/// </summary>
public static class KyrolusQueryableProjectionExtensions
{
    private static readonly ConcurrentDictionary<(Type Source, Type Target), LambdaExpression> _projectionCache = new();

    /// <summary>
    /// Projects an <see cref="IQueryable{TSource}"/> sequence into <see cref="IQueryable{TTarget}"/> using a compiled LINQ projection expression.
    /// </summary>
    /// <typeparam name="TSource">The source database entity type.</typeparam>
    /// <typeparam name="TTarget">The destination DTO type.</typeparam>
    /// <param name="source">The source queryable.</param>
    /// <param name="mapper">The object mapper instance supplying configuration rules.</param>
    /// <returns>An <see cref="IQueryable{TTarget}"/> projecting only the required columns from the database.</returns>
    public static IQueryable<TTarget> ProjectTo<TSource, TTarget>(this IQueryable<TSource> source, IKyrolusObjectMapper? mapper = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var expr = GetOrCreateProjectionExpression<TSource, TTarget>();
        return source.Select(expr);
    }

    /// <summary>
    /// Projects a weakly-typed <see cref="IQueryable"/> sequence to <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TTarget">The destination DTO type.</typeparam>
    /// <param name="source">The source queryable.</param>
    /// <param name="mapper">The object mapper instance supplying configuration rules.</param>
    /// <returns>An <see cref="IQueryable{TTarget}"/> projection query.</returns>
    public static IQueryable<TTarget> ProjectTo<TTarget>(this IQueryable source, IKyrolusObjectMapper? mapper = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sourceType = source.ElementType;
        var method = typeof(KyrolusQueryableProjectionExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(ProjectTo) && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(sourceType, typeof(TTarget));

        return (IQueryable<TTarget>)method.Invoke(null, [source, mapper])!;
    }

    /// <summary>
    /// Creates or retrieves a cached LINQ <see cref="Expression{TDelegate}"/> projecting <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>.
    /// </summary>
    public static Expression<Func<TSource, TTarget>> GetOrCreateProjectionExpression<TSource, TTarget>()
    {
        var lambda = _projectionCache.GetOrAdd((typeof(TSource), typeof(TTarget)), static key => BuildProjectionLambda(key.Source, key.Target));
        return (Expression<Func<TSource, TTarget>>)lambda;
    }

    private static LambdaExpression BuildProjectionLambda(Type sourceType, Type targetType)
    {
        var param = Expression.Parameter(sourceType, "src");
        var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        var bindings = new List<MemberBinding>();
        foreach (var targetProp in targetProps)
        {
            if (TryCreateMemberBinding(targetProp, sourceProps, param, out var binding))
            {
                bindings.Add(binding);
            }
        }

        var newExpr = Expression.New(targetType);
        var memberInit = Expression.MemberInit(newExpr, bindings);

        return Expression.Lambda(memberInit, param);
    }

    private static bool TryCreateMemberBinding(
        PropertyInfo targetProp,
        Dictionary<string, PropertyInfo> sourceProps,
        ParameterExpression param,
        [NotNullWhen(true)] out MemberBinding? binding)
    {
        binding = null;

        if (targetProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
        {
            return false;
        }

        var mapAttr = targetProp.GetCustomAttribute<KyrolusMapPropertyAttribute>();
        var sourcePropName = mapAttr?.SourceName ?? targetProp.Name;

        if (!sourceProps.TryGetValue(sourcePropName, out var sourceProp) ||
            sourceProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
        {
            return false;
        }

        Expression sourceMemberAccess = Expression.Property(param, sourceProp);

        if (targetProp.PropertyType != sourceProp.PropertyType)
        {
            if (!targetProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
            {
                return false;
            }

            sourceMemberAccess = Expression.Convert(sourceMemberAccess, targetProp.PropertyType);
        }

        binding = Expression.Bind(targetProp, sourceMemberAccess);
        return true;
    }
}
