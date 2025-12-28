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
                : Expression.Constant(Convert.ChangeType(value, left.Type), left.Type);

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
                : Expression.Constant(Convert.ChangeType(sourceValue, left.Type), left.Type);

            var equal = Expression.Equal(left, right);
            body = body == null ? equal : Expression.AndAlso(body, equal);
        }

        return Expression.Lambda<Func<TEntity, bool>>(body!, parameter);
    }
}
