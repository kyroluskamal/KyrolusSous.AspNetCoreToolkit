using System.Linq.Expressions;

namespace KyrolusSous.Elasticsearch;

public static class ExpressionHelper
{
    public static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        LambdaExpression lambda = expression;
        var memberExpression = lambda.Body switch
        {
            UnaryExpression unary => unary.Operand as MemberExpression,
            MemberExpression member => member,
            _ => null
        };

        if (memberExpression is null)
        {
            return string.Empty;
        }

        var propName = memberExpression.Member.Name;
        return char.ToLowerInvariant(propName[0]) + propName[1..];
    }
}
