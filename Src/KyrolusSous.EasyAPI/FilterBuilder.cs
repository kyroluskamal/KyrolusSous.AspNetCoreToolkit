global using System.Linq.Expressions;
global using System.Reflection;
using System.Text.RegularExpressions;

namespace KyrolusSous.EasyAPI;

public static partial class FilterBuilder
{
    public static Expression<Func<TEntity, bool>>? BuildFilterExpression<TEntity>(string? filter)
    {
        if (string.IsNullOrEmpty(filter)) return null;

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var tokens = TokenizeFilter(filter);
        if (tokens == null || tokens.Count == 0) return null;

        var combinedExpression = CombineExpressions<TEntity>(tokens, parameter);
        if (combinedExpression == null) return null;

        return Expression.Lambda<Func<TEntity, bool>>(combinedExpression, parameter);
    }

    private static Expression? CombineExpressions<TEntity>(List<string> tokens, ParameterExpression parameter)
    {
        Expression? combinedExpression = null;
        string? pendingOperator = null;

        foreach (var token in tokens)
        {
            if (token == "," || token == "|")
            {
                pendingOperator = token;
            }
            else
            {
                var conditionExpression = BuildConditionExpression<TEntity>(token, parameter);
                if (conditionExpression == null) continue;

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
    private static BinaryExpression? BuildConditionExpression<TEntity>(string filterPart, ParameterExpression parameter)
    {
        // Use regex or custom parsing to extract property name, operator, and value
        var filterMatch = MyRegex().Match(filterPart);
        if (!filterMatch.Success) return null;

        string propertyName = filterMatch.Groups[1].Value;
        string @operator = filterMatch.Groups[2].Value;
        string value = filterMatch.Groups[3].Value;

        // Get the property info
        var entityType = typeof(TEntity);
        var property = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null) throw new ArgumentException($"Property {propertyName} not found on {entityType.Name}");

        // Convert the value to the correct type
        var propertyType = property.PropertyType;
        var constantValue = Expression.Constant(Convert.ChangeType(value, propertyType));

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

    [GeneratedRegex(@"^(\w+)(==|!=|>=|<=|>|<)(.+)$")]
    private static partial Regex MyRegex();
}
