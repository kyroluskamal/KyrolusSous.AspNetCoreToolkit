using System.Globalization;
using System.Reflection;
using KyrolusSous.Repositories.EF.Abstractions.Dynamic;

namespace KyrolusSous.Repositories.EF.Runtime.Dynamic;

/// <summary>
/// Provides dynamic string-based sorting and filtering query extensions on <see cref="IQueryable{TEntity}"/>.
/// </summary>
public static class KyrolusDynamicQueryExtensions
{
    private static readonly ConcurrentDictionary<(Type EntityType, string SortExpr), List<KyrolusSortField>> _sortCache = new();

    /// <summary>
    /// Applies dynamic sorting to an <see cref="IQueryable{TEntity}"/> based on a comma-separated sort string (e.g. <c>"Price desc, CreatedAtUtc asc"</c>).
    /// </summary>
    public static IQueryable<TEntity> ApplyDynamicSort<TEntity>(this IQueryable<TEntity> query, string? sortExpression)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(sortExpression))
        {
            return query;
        }

        var fields = _sortCache.GetOrAdd((typeof(TEntity), sortExpression), static key => ParseSortExpression(key.SortExpr));
        if (fields.Count == 0)
        {
            return query;
        }

        IOrderedQueryable<TEntity>? orderedQuery = null;

        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var property = typeof(TEntity).GetProperty(field.PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                continue;
            }

            var param = Expression.Parameter(typeof(TEntity), "e");
            var propertyAccess = Expression.Property(param, property);
            var lambda = Expression.Lambda(propertyAccess, param);

            var methodName = i == 0 ? "OrderBy" : "ThenBy";
            if (field.Direction == KyrolusSortDirection.Descending)
            {
                methodName += "Descending";
            }

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity), property.PropertyType);

            orderedQuery = (IOrderedQueryable<TEntity>)method.Invoke(null, [orderedQuery ?? query, lambda])!;
        }

        return orderedQuery ?? query;
    }

    /// <summary>
    /// Applies a dynamic binary comparison filter to an <see cref="IQueryable{TEntity}"/>.
    /// </summary>
    public static IQueryable<TEntity> ApplyDynamicFilter<TEntity>(
        this IQueryable<TEntity> query,
        string propertyName,
        KyrolusFilterOperator op,
        object? value)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var property = typeof(TEntity).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null)
        {
            return query;
        }

        var param = Expression.Parameter(typeof(TEntity), "e");
        var propertyAccess = Expression.Property(param, property);
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var convertedValue = ConvertFilterValue(value, targetType);
        var constant = Expression.Constant(convertedValue, property.PropertyType);

        Expression body = op switch
        {
            KyrolusFilterOperator.Equals => Expression.Equal(propertyAccess, constant),
            KyrolusFilterOperator.NotEquals => Expression.NotEqual(propertyAccess, constant),
            KyrolusFilterOperator.GreaterThan => Expression.GreaterThan(propertyAccess, constant),
            KyrolusFilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(propertyAccess, constant),
            KyrolusFilterOperator.LessThan => Expression.LessThan(propertyAccess, constant),
            KyrolusFilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(propertyAccess, constant),
            KyrolusFilterOperator.Contains when property.PropertyType == typeof(string) =>
                Expression.Call(propertyAccess, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, constant),
            KyrolusFilterOperator.StartsWith when property.PropertyType == typeof(string) =>
                Expression.Call(propertyAccess, typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!, constant),
            KyrolusFilterOperator.EndsWith when property.PropertyType == typeof(string) =>
                Expression.Call(propertyAccess, typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!, constant),
            _ => Expression.Equal(propertyAccess, constant)
        };

        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, param);
        return query.Where(lambda);
    }

    private static object? ConvertFilterValue(object? value, Type targetType)
    {
        if (value is null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        if (targetType == typeof(Guid) && value is string strGuid)
            return Guid.TryParse(strGuid, out var g) ? g : default;

        if (targetType == typeof(DateOnly) && value is string strDate)
            return DateOnly.TryParse(strDate, CultureInfo.InvariantCulture, out var d) ? d : default;

        if (targetType == typeof(TimeOnly) && value is string strTime)
            return TimeOnly.TryParse(strTime, CultureInfo.InvariantCulture, out var t) ? t : default;

        if (targetType.IsEnum)
        {
            if (value is string strEnum) return Enum.Parse(targetType, strEnum, ignoreCase: true);
            return Enum.ToObject(targetType, value);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private static List<KyrolusSortField> ParseSortExpression(string sortExpr)
    {
        var result = new List<KyrolusSortField>();
        var segments = sortExpr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            var parts = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var propName = parts[0];
            var direction = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? KyrolusSortDirection.Descending
                : KyrolusSortDirection.Ascending;

            result.Add(new KyrolusSortField(propName, direction));
        }

        return result;
    }
}
