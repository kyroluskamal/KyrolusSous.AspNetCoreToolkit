global using System.Linq.Expressions;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KyrolusSous.EndpointKit.EF;

public static partial class FilterBuilder
{
    public static Expression<Func<TEntity, bool>>? BuildFilterExpression<TEntity>(string? filter)
    {
        _ = TryBuildFilterExpression<TEntity>(filter, null, false, out var expression, out _);
        return expression;
    }

    public static bool TryBuildFilterExpression<TEntity>(
        string? filter,
        ISet<string>? allowedProperties,
        bool strict,
        out Expression<Func<TEntity, bool>>? expression,
        out string? error)
    {
        expression = null;
        error = null;
        if (string.IsNullOrWhiteSpace(filter)) return true;

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var tokens = TokenizeFilter(filter);
        if (tokens == null || tokens.Count == 0) return true;

        var combinedExpression = CombineExpressions<TEntity>(tokens, parameter, allowedProperties, strict, out error);
        if (combinedExpression == null) return error is null;

        expression = Expression.Lambda<Func<TEntity, bool>>(combinedExpression, parameter);
        return true;
    }

    public static bool TryBuildFilterExpression<TEntity>(
        IReadOnlyList<FilterClause>? clauses,
        ISet<string>? allowedProperties,
        bool strict,
        out Expression<Func<TEntity, bool>>? expression,
        out string? error)
    {
        expression = null;
        error = null;
        if (clauses is null || clauses.Count == 0) return true;

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        Expression? combined = null;
        foreach (var clause in clauses)
        {
            if (!IsAllowed(allowedProperties, clause.Property))
            {
                if (strict)
                {
                    error = $"Filtering by '{clause.Property}' is not allowed.";
                    return false;
                }
                continue;
            }

            if (!TryBuildConditionExpression<TEntity>(clause, parameter, out var condition, out error))
            {
                return false;
            }

            combined = combined is null ? condition : Expression.AndAlso(combined, condition);
        }

        if (combined is null) return true;
        expression = Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
        return true;
    }

    private static Expression? CombineExpressions<TEntity>(List<string> tokens, ParameterExpression parameter, ISet<string>? allowedProperties, bool strict, out string? error)
    {
        Expression? combinedExpression = null;
        string? pendingOperator = null;
        error = null;

        foreach (var token in tokens)
        {
            if (token == "," || token == "|")
            {
                pendingOperator = token;
            }
            else
            {
                var conditionExpression = BuildConditionExpression<TEntity>(token, parameter, allowedProperties, strict, out error);
                if (conditionExpression == null)
                {
                    if (error is not null && strict) return null;
                    continue;
                }

                combinedExpression = CombineWithOperator(combinedExpression, conditionExpression, pendingOperator);
                pendingOperator = null;
            }
        }

        return combinedExpression;
    }

    private static Expression? CombineWithOperator(Expression? combinedExpression, Expression conditionExpression, string? pendingOperator)
    {
        if (combinedExpression == null)
        {
            return conditionExpression;
        }

        return pendingOperator switch
        {
            "," => Expression.AndAlso(combinedExpression, conditionExpression),
            "|" => Expression.OrElse(combinedExpression, conditionExpression),
            _ => combinedExpression
        };
    }

    // Tokenizes the filter string based on ',' for AND and '|' for OR
    private static List<string> TokenizeFilter(string filter)
    {
        var tokens = new List<string>();
        int lastPos = 0;

        for (int i = 0; i < filter.Length; i++)
        {
            if (filter[i] == ',' || filter[i] == '|')
            {
                tokens.Add(filter[lastPos..i].Trim()); // Add the condition before the operator
                tokens.Add(filter[i].ToString()); // Add the operator (',' or '|')
                lastPos = i + 1;
            }
        }

        if (lastPos < filter.Length)
        {
            tokens.Add(filter[lastPos..].Trim()); // Add the final condition
        }

        return tokens;
    }

    // Builds the individual condition expressions (e.g., x => x.Name == "John")
    private static BinaryExpression? BuildConditionExpression<TEntity>(
        string filterPart,
        ParameterExpression parameter,
        ISet<string>? allowedProperties,
        bool strict,
        out string? error)
    {
        error = null;
        // Use regex or custom parsing to extract property name, operator, and value
        var filterMatch = MyRegex().Match(filterPart);
        if (!filterMatch.Success) return null;

        string propertyName = filterMatch.Groups[1].Value;
        string @operator = filterMatch.Groups[2].Value;
        string value = filterMatch.Groups[3].Value;

        if (!IsAllowed(allowedProperties, propertyName))
        {
            if (strict) error = $"Filtering by '{propertyName}' is not allowed.";
            return null;
        }

        // Get the property info
        var entityType = typeof(TEntity);
        var property = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            error = $"Property '{propertyName}' was not found on {entityType.Name}.";
            return null;
        }

        // Convert the value to the correct type
        var propertyType = property.PropertyType;
        if (!TryConvert(value, propertyType, out var typedValue))
        {
            error = $"Value '{value}' could not be converted to {propertyType.Name}.";
            return null;
        }

        var constantValue = Expression.Constant(typedValue, propertyType);

        // Build the individual comparison expression based on the operator
        return @operator switch
        {
            "==" => Expression.Equal(Expression.MakeMemberAccess(parameter, property), constantValue),
            "!=" => Expression.NotEqual(Expression.MakeMemberAccess(parameter, property), constantValue),
            ">" => Expression.GreaterThan(Expression.MakeMemberAccess(parameter, property), constantValue),
            "<" => Expression.LessThan(Expression.MakeMemberAccess(parameter, property), constantValue),
            ">=" => Expression.GreaterThanOrEqual(Expression.MakeMemberAccess(parameter, property), constantValue),
            "<=" => Expression.LessThanOrEqual(Expression.MakeMemberAccess(parameter, property), constantValue),
            _ => throw new ArgumentException($"Unsupported operator '{@operator}'")
        };
    }

    private static bool TryBuildConditionExpression<TEntity>(
        FilterClause clause,
        ParameterExpression parameter,
        out BinaryExpression condition,
        out string? error)
    {
        error = null;
        condition = null!;
        var entityType = typeof(TEntity);
        var property = entityType.GetProperty(clause.Property, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            error = $"Property '{clause.Property}' was not found on {entityType.Name}.";
            return false;
        }

        if (!TryConvert(clause.Value, property.PropertyType, out var typedValue))
        {
            error = $"Value '{clause.Value}' could not be converted to {property.PropertyType.Name}.";
            return false;
        }

        var member = Expression.MakeMemberAccess(parameter, property);
        var constant = Expression.Constant(typedValue, property.PropertyType);
        var op = clause.Operator?.Trim().ToLowerInvariant();

        condition = op switch
        {
            "==" or "eq" => Expression.Equal(member, constant),
            "!=" or "neq" => Expression.NotEqual(member, constant),
            ">" or "gt" => Expression.GreaterThan(member, constant),
            "<" or "lt" => Expression.LessThan(member, constant),
            ">=" or "gte" => Expression.GreaterThanOrEqual(member, constant),
            "<=" or "lte" => Expression.LessThanOrEqual(member, constant),
            "contains" => BuildStringCall(member, nameof(string.Contains), constant),
            "startswith" => BuildStringCall(member, nameof(string.StartsWith), constant),
            "endswith" => BuildStringCall(member, nameof(string.EndsWith), constant),
            _ => throw new ArgumentException($"Unsupported operator '{clause.Operator}'")
        };

        return true;
    }

    private static BinaryExpression BuildStringCall(Expression member, string methodName, Expression constant)
    {
        if (member.Type != typeof(string))
        {
            throw new ArgumentException($"Operator '{methodName}' is only valid for string properties.");
        }

        var call = Expression.Call(member, typeof(string).GetMethod(methodName, [typeof(string)])!, constant);
        return Expression.Equal(call, Expression.Constant(true));
    }

    private static bool TryConvert(string? raw, Type targetType, out object? result)
    {
        result = null;
        if (raw is null)
        {
            return true;
        }

        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
        {
            result = null;
            return true;
        }

        if (nonNullableType == typeof(string))
        {
            result = raw.Trim('"');
            return true;
        }

        if (nonNullableType == typeof(Guid))
        {
            if (Guid.TryParse(raw, out var guid))
            {
                result = guid;
                return true;
            }
            return false;
        }

        if (nonNullableType == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            {
                result = dto;
                return true;
            }
            return false;
        }

        if (nonNullableType == typeof(DateTime))
        {
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                result = dt;
                return true;
            }
            return false;
        }

        if (nonNullableType == typeof(DateOnly))
        {
            if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            {
                result = dateOnly;
                return true;
            }
            return false;
        }

        if (nonNullableType == typeof(TimeOnly))
        {
            if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly))
            {
                result = timeOnly;
                return true;
            }
            return false;
        }

        if (nonNullableType.IsEnum)
        {
            try
            {
                result = Enum.Parse(nonNullableType, raw, ignoreCase: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            result = Convert.ChangeType(raw, nonNullableType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAllowed(ISet<string>? allowlist, string property)
        => allowlist is null || allowlist.Count == 0 || allowlist.Contains(property);

    [GeneratedRegex(@"^(\w+)(==|!=|>=|<=|>|<)(.+)$")]
    private static partial Regex MyRegex();
}
