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
            current = Expression.PropertyOrField(current, segment);

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

        if (underlying.IsInstanceOfType(value))
            return value;

        if (TryConvertKnownTypes(value, underlying, out var converted))
            return converted;

        if (TryConvertEnum(value, underlying, out converted))
            return converted;

        if (TryConvertConvertible(value, underlying, out converted))
            return converted;

        return value;
    }

    private static bool TryConvertKnownTypes(object value, Type underlying, out object? result)
    {
        if (underlying == typeof(string))
        {
            result = value.ToString();
            return true;
        }

        if (underlying == typeof(Guid))
            return TryConvertGuid(value, out result);

        if (underlying == typeof(DateTimeOffset))
            return TryConvertDateTimeOffset(value, out result);

        if (underlying == typeof(DateTime))
            return TryConvertDateTime(value, out result);

        if (underlying == typeof(TimeSpan))
            return TryConvertTimeSpan(value, out result);

        result = null;
        return false;
    }

    private static bool TryConvertGuid(object value, out object? result)
    {
        if (value is Guid g)
        {
            result = g;
            return true;
        }

        if (value is string s && Guid.TryParse(s, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertDateTimeOffset(object value, out object? result)
    {
        if (value is DateTimeOffset dto)
        {
            result = dto;
            return true;
        }

        if (value is string s && DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertDateTime(object value, out object? result)
    {
        if (value is DateTime dt)
        {
            result = dt;
            return true;
        }

        if (value is string s && DateTime.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertTimeSpan(object value, out object? result)
    {
        if (value is TimeSpan ts)
        {
            result = ts;
            return true;
        }

        if (value is string s && TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertEnum(object value, Type underlying, out object? result)
    {
        if (!underlying.IsEnum)
        {
            result = null;
            return false;
        }
        if (value is string s)
        {
            result = Enum.Parse(underlying, s, ignoreCase: true);
            return true;
        }

        result = Enum.ToObject(underlying, value);
        return true;
    }

    private static bool TryConvertConvertible(object value, Type underlying, out object? result)
    {
        if (value is not IConvertible)
        {
            result = null;
            return false;
        }

        result = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return true;
    }
}
