using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace KyrolusSous.Repositories.Marten.Runtime.Dynamic;

/// <summary>
/// Provides dynamic JSON document filtering and sorting extensions on Marten <see cref="IQueryable{TDocument}"/>.
/// </summary>
public static class KyrolusMartenDynamicQueryExtensions
{
    /// <summary>
    /// Applies dynamic sorting to an <see cref="IQueryable{TDocument}"/> based on a comma-separated sort expression.
    /// </summary>
    public static IQueryable<TDocument> ApplyMartenDynamicSort<TDocument>(
        this IQueryable<TDocument> query,
        string? sortExpression)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(sortExpression))
        {
            return query;
        }

        var segments = sortExpression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IOrderedQueryable<TDocument>? orderedQuery = null;

        for (var i = 0; i < segments.Length; i++)
        {
            var parts = segments[i].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) continue;

            var propName = parts[0];
            var isDescending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

            var property = typeof(TDocument).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null) continue;

            var param = Expression.Parameter(typeof(TDocument), "doc");
            var propertyAccess = Expression.Property(param, property);
            var lambda = Expression.Lambda(propertyAccess, param);

            var methodName = i == 0
                ? (isDescending ? "OrderByDescending" : "OrderBy")
                : (isDescending ? "ThenByDescending" : "ThenBy");

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TDocument), property.PropertyType);

            orderedQuery = (IOrderedQueryable<TDocument>)method.Invoke(null, [orderedQuery ?? query, lambda])!;
        }

        return orderedQuery ?? query;
    }

    /// <summary>
    /// Applies a dynamic equality or comparison filter to an <see cref="IQueryable{TDocument}"/>.
    /// </summary>
    public static IQueryable<TDocument> ApplyMartenDynamicFilter<TDocument>(
        this IQueryable<TDocument> query,
        string propertyName,
        string op,
        object? value)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var property = typeof(TDocument).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null) return query;

        var param = Expression.Parameter(typeof(TDocument), "doc");
        var propertyAccess = Expression.Property(param, property);
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var convertedValue = ConvertFilterValue(value, targetType);
        var constant = Expression.Constant(convertedValue, property.PropertyType);

        Expression body = op.ToLowerInvariant() switch
        {
            "eq" or "==" or "equals" => Expression.Equal(propertyAccess, constant),
            "neq" or "!=" => Expression.NotEqual(propertyAccess, constant),
            "gt" or ">" => Expression.GreaterThan(propertyAccess, constant),
            "gte" or ">=" => Expression.GreaterThanOrEqual(propertyAccess, constant),
            "lt" or "<" => Expression.LessThan(propertyAccess, constant),
            "lte" or "<=" => Expression.LessThanOrEqual(propertyAccess, constant),
            _ => Expression.Equal(propertyAccess, constant)
        };

        var lambda = Expression.Lambda<Func<TDocument, bool>>(body, param);
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
}
