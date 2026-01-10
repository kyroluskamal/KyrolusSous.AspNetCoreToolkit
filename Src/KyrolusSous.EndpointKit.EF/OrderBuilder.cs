using KyrolusSous.Repositories.EF.Abstractions.Query;
using System.Linq.Expressions;
using System.Reflection;

namespace KyrolusSous.EndpointKit.EF;

public static class OrderBuilder
{
    public static Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? BuildOrderBy<TEntity>(
        string? orderBy,
        ISet<string>? allowedProperties,
        bool strict,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return null;
        }

        var clauses = orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseClause)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToArray();

        return BuildOrderBy<TEntity>(clauses, allowedProperties, strict, out error);
    }

    public static Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? BuildOrderBy<TEntity>(
        IReadOnlyList<OrderClause>? clauses,
        ISet<string>? allowedProperties,
        bool strict,
        out string? error)
    {
        error = null;
        if (clauses is null || clauses.Count == 0)
        {
            return null;
        }

        var allowedSet = NormalizeAllowlist(allowedProperties);
        foreach (var clause in clauses)
        {
            if (!IsAllowed(allowedSet, clause.Property))
            {
                error = $"Ordering by '{clause.Property}' is not allowed.";
                return null;
            }
        }

        return query =>
        {
            IOrderedQueryable<TEntity>? ordered = null;
            for (var i = 0; i < clauses.Count; i++)
            {
                var clause = clauses[i];
                var (segments, memberType) = ResolveMember<TEntity>(clause.Property);
                var parameter = Expression.Parameter(typeof(TEntity), "x");
                var access = BuildMemberAccess(parameter, segments);
                var lambda = Expression.Lambda(access, parameter);

                var isFirst = i == 0;
                var methodName = GetMethodName(isFirst, clause.Desc);
                var method = GetQueryableMethod(methodName, typeof(TEntity), memberType);
                var result = method.Invoke(null, [isFirst ? query : ordered!, lambda])!;
                ordered = (IOrderedQueryable<TEntity>)result;
            }

            return ordered!;
        };
    }

    private static OrderClause? ParseClause(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var property = parts[0];
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
        return new OrderClause(property, desc);
    }

    private static string GetMethodName(bool first, bool desc)
        => first
            ? (desc ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
            : (desc ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

    private static MethodInfo GetQueryableMethod(string name, Type entityType, Type memberType)
    {
        return typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType, memberType);
    }

    private static (string[] Segments, Type MemberType) ResolveMember<TEntity>(string propertyPath)
    {
        var entityType = typeof(TEntity);
        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        PropertyInfo? property = null;
        Type current = entityType;

        foreach (var segment in segments)
        {
            property = current.GetProperty(segment, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"Property '{propertyPath}' was not found on {entityType.Name}.");
            current = property.PropertyType;
        }

        return (segments, current);
    }

    private static Expression BuildMemberAccess(Expression parameter, string[] segments)
    {
        Expression current = parameter;
        foreach (var segment in segments)
        {
            current = Expression.PropertyOrField(current, segment);
        }
        return current;
    }

    private static bool IsAllowed(ISet<string>? allowed, string property)
        => allowed is null || allowed.Count == 0 || allowed.Contains(property);

    private static ISet<string>? NormalizeAllowlist(ISet<string>? allowlist)
        => allowlist is null || allowlist.Count == 0 ? null : new HashSet<string>(allowlist, StringComparer.OrdinalIgnoreCase);
}
