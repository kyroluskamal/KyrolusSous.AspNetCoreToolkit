using System.Globalization;

namespace KyrolusSous.Repositories.EF.Abstractions.Helpers;

public class KyrolusEFRepositoryBase<TEntity>
{
    public static Expression<Func<TEntity, object?>>? BuildIncludeExpression(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath)) return null;
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression current = parameter;
        foreach (var segment in propertyPath.Split('.'))
        {
            current = Expression.PropertyOrField(current, segment);
        }
        var body = Expression.Convert(current, typeof(object));
        return Expression.Lambda<Func<TEntity, object?>>(body, parameter);
    }
    public static Expression<Func<TEntity, object?>>[]? ConvertIncludePropertiesToExpressions(List<string>? includeProperties)
    {
        return includeProperties?.Select(p => BuildIncludeExpression(p))
             .Where(e => e != null)
             .Select(e => e!)
             .ToArray() ?? [];
    }
    public static Expression<Func<TEntity, bool>> GetPrimaryKeyFromKeyValues(object?[] keyValues, string[] KeyPropertyNames)
    {
        if (KeyPropertyNames.Length != keyValues.Length)
            throw new ArgumentException("Number of key values does not match primary key properties.", nameof(keyValues));
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression? body = null;

        for (int i = 0; i < KeyPropertyNames.Length; i++)
        {
            var prop = KeyPropertyNames[i];
            var left = Expression.Property(parameter, prop);
            var value = keyValues[i];
            var convertedValue = value == null
                ? Expression.Constant(null, left.Type)
                : Expression.Constant(ConvertToType(value, left.Type), left.Type);

            var equal = Expression.Equal(left, convertedValue);
            body = body == null ? equal : Expression.AndAlso(body, equal);
        }

        return Expression.Lambda<Func<TEntity, bool>>(body!, parameter);
    }

    public static Expression<Func<TEntity, bool>> BuildKeyPredicateFromEntity(TEntity source, string[] keyProps)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression? body = null;

        foreach (var prop in keyProps)
        {
            var left = Expression.Property(parameter, prop);
            var sourceValue = typeof(TEntity).GetProperty(prop)!.GetValue(source);
            var right = sourceValue == null
                ? Expression.Constant(null, left.Type)
                : Expression.Constant(ConvertToType(sourceValue, left.Type), left.Type);

            var equal = Expression.Equal(left, right);
            body = body == null ? equal : Expression.AndAlso(body, equal);
        }

        return Expression.Lambda<Func<TEntity, bool>>(body!, parameter);
    }

    private static object? ConvertToType(object? value, Type targetType)
    {
        if (value is null) return null;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying.IsInstanceOfType(value)) return value;

        if (underlying == typeof(string)) return value.ToString();

        if (underlying == typeof(Guid))
        {
            if (value is Guid guid) return guid;
            if (value is string guidText && Guid.TryParse(guidText, out var parsed)) return parsed;
        }

        if (underlying == typeof(DateTimeOffset))
        {
            if (value is DateTimeOffset dto) return dto;
            if (value is string text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed;
            }
        }

        if (underlying == typeof(DateTime))
        {
            if (value is DateTime dt) return dt;
            if (value is string text && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed;
            }
        }

        if (underlying == typeof(TimeSpan))
        {
            if (value is TimeSpan ts) return ts;
            if (value is string text && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        }

        if (underlying.IsEnum)
        {
            if (value is string enumText) return Enum.Parse(underlying, enumText, true);
            return Enum.ToObject(underlying, value);
        }

        if (value is IConvertible)
        {
            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }

        return value;
    }
}
