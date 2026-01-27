using System.Globalization;
using KyrolusSous.Repositories.EF.Abstractions.Query;

namespace KyrolusSous.Repositories.EF.Runtime;

public sealed class RuntimeQueryHelper<TEntity> : IQueryHelper<TEntity>
{
    public QueryParts<TEntity> Build(QueryRequest? request)
    {
        request ??= new QueryRequest();
        return new QueryParts<TEntity>(
            Filter: BuildFilter(request),
            OrderBy: BuildOrderBy(request),
            Includes: BuildIncludes(request),
            AsNoTracking: request.AsNoTracking,
            UseSplitQuery: request.UseSplitQuery,
            IncludeGraph: request.IncludeGraph as IncludeGraph<TEntity>);
    }

    public Expression<Func<TEntity, bool>>? BuildFilter(QueryRequest? request)
    {
        var filters = request?.Filters;
        if (filters is null || filters.Length == 0)
            return null;

        var param = Expression.Parameter(typeof(TEntity), "e");
        Expression? body = null;

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Property))
                continue;

            var left = BuildPropertyExpression(param, filter.Property);
            if (left is null)
                continue;

            if (!TryBuildPredicate(left, filter.Operator, filter.Value, out var predicate))
                continue;

            body = body is null ? predicate : Expression.AndAlso(body, predicate);
        }

        return body is null ? null : Expression.Lambda<Func<TEntity, bool>>(body, param);
    }

    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? BuildOrderBy(QueryRequest? request)
    {
        var clauses = request?.OrderBy;
        if (clauses is null || clauses.Length == 0)
            return null;

        var selectors = new List<(Expression Body, Type BodyType, LambdaExpression Lambda, bool Desc)>();
        foreach (var clause in clauses)
        {
            if (string.IsNullOrWhiteSpace(clause.Property))
                continue;

            var param = Expression.Parameter(typeof(TEntity), "e");
            var body = BuildPropertyExpression(param, clause.Property);
            if (body is null)
                continue;

            var delegateType = typeof(Func<,>).MakeGenericType(typeof(TEntity), body.Type);
            var lambda = Expression.Lambda(delegateType, body, param);
            selectors.Add((body, body.Type, lambda, clause.Desc));
        }

        if (selectors.Count == 0)
            return null;

        return query => ApplyOrdering(query, selectors);
    }

    private static IOrderedQueryable<TEntity> ApplyOrdering(IQueryable<TEntity> query, List<(Expression Body, Type BodyType, LambdaExpression Lambda, bool Desc)> selectors)
    {
        IOrderedQueryable<TEntity>? ordered = null;
        var isFirst = true;

        foreach (var (_, bodyType, lambda, desc) in selectors)
        {
            string methodName;
            if (isFirst)
            {
                methodName = desc ? "OrderByDescending" : "OrderBy";
                isFirst = false;
            }
            else
            {
                methodName = desc ? "ThenByDescending" : "ThenBy";
            }

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2);

            var generic = method.MakeGenericMethod(typeof(TEntity), bodyType);
            var result = generic.Invoke(null, [ordered ?? query, lambda]);
            ordered = (IOrderedQueryable<TEntity>)result!;
        }

        return ordered ?? query.OrderBy(_ => 0);
    }

    public Expression<Func<TEntity, object?>>[] BuildIncludes(QueryRequest? request) => [];

    private static Expression? BuildPropertyExpression(Expression param, string propertyPath)
    {
        var parts = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        Expression current = param;
        foreach (var part in parts)
        {
            var prop = current.Type.GetProperty(part);
            if (prop is null)
                return null;

            current = Expression.Property(current, prop);
        }

        return current;
    }

    private static bool TryBuildPredicate(Expression left, string opRaw, string? rawValue, out Expression predicate)
    {
        predicate = default!;
        var op = NormalizeOperator(opRaw);

        if (rawValue is null)
            return TryBuildNullPredicate(left, op, out predicate);

        if (!TryConvertValue(rawValue, left.Type, out var value))
            return false;

        Expression constant = Expression.Constant(value, value?.GetType() ?? left.Type);
        if (constant.Type != left.Type)
            constant = Expression.Convert(constant, left.Type);

        if (IsEqualityOperator(op))
        {
            predicate = BuildEquality(op, left, constant);
            return predicate is not null;
        }

        if (TryBuildRelational(op, left, constant, out predicate))
            return true;

        if (IsStringOperator(op))
        {
            if (left.Type != typeof(string))
                return false;

            var methodName = op switch
            {
                "contains" => nameof(string.Contains),
                "startswith" => nameof(string.StartsWith),
                "endswith" => nameof(string.EndsWith),
                _ => null
            };

            if (methodName is null)
                return false;

            predicate = BuildStringCall(left, constant, methodName);
            return true;
        }

        return false;
    }

    private static bool TryBuildNullPredicate(Expression left, string op, out Expression predicate)
    {
        predicate = default!;
        if (left.Type.IsValueType && Nullable.GetUnderlyingType(left.Type) is null)
            return false;

        var nullConst = Expression.Constant(null, left.Type);
        if (IsEqualityOperator(op))
        {
            predicate = BuildEquality(op, left, nullConst);
            return predicate is not null;
        }

        return false;
    }

    private static bool IsEqualityOperator(string op)
        => op is "eq" or "==" or "=" or "neq" or "!=" or "<>";

    private static BinaryExpression BuildEquality(string op, Expression left, Expression right)
    {
        return op switch
        {
            "eq" or "==" or "=" => Expression.Equal(left, right),
            "neq" or "!=" or "<>" => Expression.NotEqual(left, right),
            _ => throw new InvalidOperationException($"Unsupported equality operator: {op}")
        };
    }

    private static bool TryBuildRelational(string op, Expression left, Expression right, out Expression predicate)
    {
        predicate = default!;
        var comparer = op switch
        {
            "gt" or ">" => (Func<Expression, Expression, Expression>)Expression.GreaterThan,
            "gte" or ">=" => Expression.GreaterThanOrEqual,
            "lt" or "<" => Expression.LessThan,
            "lte" or "<=" => Expression.LessThanOrEqual,
            _ => null
        };

        if (comparer is null)
            return false;

        predicate = comparer(left, right);
        return true;
    }

    private static bool IsStringOperator(string op)
        => op is "contains" or "startswith" or "endswith";

    private static MethodCallExpression BuildStringCall(Expression left, Expression value, string methodName)
    {
        var valueExpr = value.Type == typeof(string) ? value : Expression.Convert(value, typeof(string));
        var method = typeof(string).GetMethod(methodName, new[] { typeof(string) });
        return Expression.Call(left, method!, valueExpr);
    }

    private static string NormalizeOperator(string? op)
        => string.IsNullOrWhiteSpace(op) ? "eq" : op.Trim().ToLowerInvariant();

    private static bool TryConvertValue(string raw, Type targetType, out object? value)
    {
        value = null;
        try
        {
            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (nonNullable == typeof(string))
            {
                value = raw;
                return true;
            }

            if (nonNullable == typeof(Guid))
            {
                value = Guid.Parse(raw);
                return true;
            }

            if (nonNullable == typeof(DateTimeOffset))
            {
                value = DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture);
                return true;
            }

            if (nonNullable == typeof(DateTime))
            {
                value = DateTime.Parse(raw, CultureInfo.InvariantCulture);
                return true;
            }

            if (nonNullable == typeof(TimeSpan))
            {
                value = TimeSpan.Parse(raw, CultureInfo.InvariantCulture);
                return true;
            }

            if (nonNullable.IsEnum)
            {
                value = Enum.Parse(nonNullable, raw, true);
                return true;
            }

            value = Convert.ChangeType(raw, nonNullable, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
