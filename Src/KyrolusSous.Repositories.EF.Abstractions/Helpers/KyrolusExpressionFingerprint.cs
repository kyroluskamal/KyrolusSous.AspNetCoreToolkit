using System.Linq.Expressions;
using System.Reflection;

namespace KyrolusSous.Repositories.EF.Abstractions.Helpers;

public static class KyrolusExpressionFingerprint
{
    public static string Build(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var normalized = new ClosureValueEvaluator().Visit(expression) ?? expression;
        return normalized.ToString();
    }

    private sealed class ClosureValueEvaluator : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (TryEvaluate(node, out var value))
            {
                return Expression.Constant(value, node.Type);
            }
            return base.VisitMember(node);
        }

        private static bool TryEvaluate(MemberExpression node, out object? value)
        {
            value = null;
            if (!TryGetTarget(node.Expression, out var target))
            {
                return false;
            }

            value = node.Member switch
            {
                FieldInfo field => field.GetValue(target),
                PropertyInfo property => property.GetValue(target),
                _ => null
            };
            return true;
        }

        private static bool TryGetTarget(Expression? expression, out object? target)
        {
            target = null;
            switch (expression)
            {
                case null:
                    return false;
                case ConstantExpression constant:
                    target = constant.Value;
                    return true;
                case MemberExpression memberExpression:
                    if (!TryEvaluate(memberExpression, out var value))
                    {
                        return false;
                    }
                    target = value;
                    return true;
                default:
                    return false;
            }
        }
    }
}
