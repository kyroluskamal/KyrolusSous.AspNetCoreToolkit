using System.Globalization;
using System.Reflection;
using System.Text;
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
                throw new ArgumentException("Invalid filter: 'Property' is required.", nameof(request));

            if (string.IsNullOrWhiteSpace(filter.Operator))
                throw new ArgumentException($"Invalid filter for property '{filter.Property}': 'Operator' is required.", nameof(request));

            var left = BuildPropertyExpression(param, filter.Property) ?? throw new ArgumentException($"Invalid filter: property='{filter.Property}', operator='{filter.Operator}', value='{filter.Value}'.", nameof(request));

            var predicate = GetPredicateForFilter(left, filter.Property, filter.Operator, filter.Value) ?? throw new ArgumentException($"Invalid filter: property='{filter.Property}', operator='{filter.Operator}', value='{filter.Value}'.", nameof(request));

            body = body is null ? predicate : Expression.AndAlso(body, predicate);
        }

        return body is null ? null : Expression.Lambda<Func<TEntity, bool>>(body, param);
    }

    private static Expression? GetPredicateForFilter(Expression left, string property, string op, string? value)
    {
        if (value is null)
        {
            if (IsNullOperator(op))
                return BuildNullCheck(left, NormalizeOperator(op) == "notnull");

            if (!IsNullComparableOperator(op))
                return null;

            return TryBuildNullPredicate(left, op, out var nullPredicate) ? nullPredicate : null;
        }

        return BuildPredicate(left, property, op, value);
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
                throw new ArgumentException("Invalid orderBy: 'Property' is required.", nameof(request));

            var param = Expression.Parameter(typeof(TEntity), "e");
            var body = BuildPropertyExpression(param, clause.Property);
            if (body is null)
                throw new ArgumentException($"Invalid orderBy: property='{clause.Property}' not found on entity '{typeof(TEntity).Name}'.", nameof(request));

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

    private static Expression? BuildPredicate(Expression left, string propertyName, string opRaw, string rawValue)
    {
        var op = NormalizeOperator(opRaw);

        var nonNullableType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;

        if (IsNullOperator(op))
        {
            if (left.Type.IsValueType && Nullable.GetUnderlyingType(left.Type) is null)
                throw new ArgumentException($"Unsupported operator '{opRaw}' for '{propertyName}'");

            return BuildNullCheck(left, op == "notnull");
        }

        if (op == "in")
        {
            var values = SplitValueList(rawValue);
            return TryBuildIn(left, left.Type, values, out var inExpr, out _) ? inExpr : null;
        }

        if (op == "between")
        {
            var values = SplitValueList(rawValue);
            return TryBuildBetween(left, left.Type, values, out var betweenExpr, out _) ? betweenExpr : null;
        }

        if (op is "any" or "all")
        {
            var values = SplitValueList(rawValue);
            return TryBuildAnyAll(left, left.Type, values, rawValue, op == "any", out var anyAllExpr, out _) ? anyAllExpr : null;
        }

        if (nonNullableType == typeof(string))
        {
            return BuildStringPredicate(left, op, rawValue);
        }

        if (!TryConvertValue(rawValue, left.Type, out var value))
            return null;

        Expression constant = Expression.Constant(value, value?.GetType() ?? left.Type);
        if (constant.Type != left.Type)
            constant = Expression.Convert(constant, left.Type);

        if (IsNumericType(nonNullableType) || IsDateType(nonNullableType))
        {
            if (IsEqualityOperator(op))
                return BuildEquality(op, left, constant);

            if (TryBuildRelational(op, left, constant, out var predicate))
                return predicate;

            throw new ArgumentException($"Unsupported operator '{opRaw}' for '{propertyName}'");
        }

        if (nonNullableType == typeof(bool) || nonNullableType == typeof(Guid) || nonNullableType.IsEnum)
        {
            if (IsEqualityOperator(op))
                return BuildEquality(op, left, constant);

            throw new ArgumentException($"Unsupported operator '{opRaw}' for '{propertyName}'");
        }

        if (IsEqualityOperator(op))
            return BuildEquality(op, left, constant);

        return null;
    }

    private static bool TryBuildNullPredicate(Expression left, string opRaw, out Expression predicate)
    {
        predicate = default!;
        var op = NormalizeOperator(opRaw);
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

    private static bool IsNullComparableOperator(string op)
        => IsEqualityOperator(NormalizeOperator(op));

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

    private static Expression? BuildStringPredicate(Expression left, string op, string rawValue)
    {
        var constant = Expression.Constant(rawValue, typeof(string));

        if (IsEqualityOperator(op))
            return BuildEquality(op, left, constant);

        var methodName = op switch
        {
            "contains" => nameof(string.Contains),
            "startswith" => nameof(string.StartsWith),
            "endswith" => nameof(string.EndsWith),
            _ => null
        };

        return methodName is null ? null : BuildStringCall(left, constant, methodName);
    }

    private static MethodCallExpression BuildStringCall(Expression left, Expression value, string methodName)
    {
        var valueExpr = value.Type == typeof(string) ? value : Expression.Convert(value, typeof(string));
        var method = typeof(string).GetMethod(methodName, new[] { typeof(string) });
        return Expression.Call(left, method!, valueExpr);
    }

    private static Expression BuildNullCheck(Expression member, bool notNull)
    {
        var nullConstant = Expression.Constant(null, member.Type);
        return notNull ? Expression.NotEqual(member, nullConstant) : Expression.Equal(member, nullConstant);
    }

    private static string NormalizeOperator(string? op)
        => string.IsNullOrWhiteSpace(op) ? "eq" : op.Trim().ToLowerInvariant();

    private static bool IsNullOperator(string op)
    {
        var normalized = NormalizeOperator(op);
        return normalized is "isnull" or "notnull";
    }

    private static bool IsNumericType(Type type)
        => type == typeof(byte) || type == typeof(sbyte)
            || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint)
            || type == typeof(long) || type == typeof(ulong)
            || type == typeof(float) || type == typeof(double)
            || type == typeof(decimal);

    private static bool IsDateType(Type type)
        => type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly);

    private static bool TryBuildIn(Expression member, Type memberType, IReadOnlyList<string?> values, out Expression expression, out string? error)
    {
        error = null;
        expression = null!;
        var elementType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (!TryConvertList(values, elementType, out var converted, out error))
        {
            return false;
        }

        var list = Array.CreateInstance(elementType, converted.Count);
        for (var i = 0; i < converted.Count; i++)
        {
            list.SetValue(converted[i], i);
        }

        var listExpr = Expression.Constant(list);
        var containsMethod = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        if (memberType != elementType)
        {
            var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
            var value = Expression.Property(member, nameof(Nullable<int>.Value));
            var containsValue = Expression.Call(containsMethod, listExpr, value);
            expression = Expression.AndAlso(hasValue, containsValue);
            return true;
        }

        expression = Expression.Call(containsMethod, listExpr, member);
        return true;
    }

    private static bool TryBuildBetween(Expression member, Type memberType, IReadOnlyList<string?> values, out Expression expression, out string? error)
    {
        error = null;
        expression = null!;
        if (values.Count < 2)
        {
            error = "Between requires two values.";
            return false;
        }

        var elementType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (!TryConvertValue(values[0] ?? string.Empty, elementType, out var start) || !TryConvertValue(values[1] ?? string.Empty, elementType, out var end))
        {
            error = $"Value could not be converted to {elementType.Name}.";
            return false;
        }

        var startConst = Expression.Constant(start, elementType);
        var endConst = Expression.Constant(end, elementType);
        Expression left = member;
        if (memberType != elementType)
        {
            left = Expression.Property(member, nameof(Nullable<int>.Value));
        }

        var ge = Expression.GreaterThanOrEqual(left, startConst);
        var le = Expression.LessThanOrEqual(left, endConst);
        var between = Expression.AndAlso(ge, le);

        if (memberType != elementType)
        {
            var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
            expression = Expression.AndAlso(hasValue, between);
            return true;
        }

        expression = between;
        return true;
    }

    private static bool TryBuildAnyAll(
        Expression member,
        Type memberType,
        IReadOnlyList<string?> values,
        string? rawContent,
        bool isAny,
        out Expression expression,
        out string? error)
    {
        error = null;
        expression = null!;

        if (!TryGetEnumerableElementType(memberType, out var elementType))
        {
            error = $"Operator '{(isAny ? "any" : "all")}' is only valid for collection properties.";
            return false;
        }

        var parameter = Expression.Parameter(elementType, "e");
        Expression? predicateBody = null;

        if (!string.IsNullOrWhiteSpace(rawContent) && LooksLikeFilter(rawContent))
        {
            if (!TryBuildNestedFilterExpression(elementType, rawContent, out var nested, out var nestedError))
            {
                error = nestedError;
                return false;
            }

            if (nested is null)
            {
                error = "Invalid nested filter.";
                return false;
            }

            predicateBody = new ReplaceParameterVisitor(nested.Parameters[0], parameter).Visit(nested.Body);
        }
        else
        {
            if (!TryConvertList(values, elementType, out var converted, out error))
            {
                return false;
            }

            var list = Array.CreateInstance(elementType, converted.Count);
            for (var i = 0; i < converted.Count; i++)
            {
                list.SetValue(converted[i], i);
            }
            var listExpr = Expression.Constant(list);
            predicateBody = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [elementType], listExpr, parameter);
        }

        var predicate = Expression.Lambda(predicateBody!, parameter);
        var methodName = isAny ? nameof(Enumerable.Any) : nameof(Enumerable.All);
        var method = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        var call = Expression.Call(method, member, predicate);
        var notNull = Expression.NotEqual(member, Expression.Constant(null, member.Type));
        expression = Expression.AndAlso(notNull, call);
        return true;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        elementType = null!;
        if (type == typeof(string)) return false;

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        var iface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (iface is null) return false;
        elementType = iface.GetGenericArguments()[0];
        return true;
    }

    private static bool LooksLikeFilter(string raw)
    {
        var span = raw.AsSpan();
        if (span.IndexOfAny("=<>".AsSpan()) >= 0) return true;
        if (span.Contains(" eq ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" neq ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" gt ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" gte ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" lt ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" lte ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" contains ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" startswith ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" endswith ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" in ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(" between ", StringComparison.OrdinalIgnoreCase)) return true;
        if (span.Contains(",", StringComparison.Ordinal) || span.Contains("|", StringComparison.Ordinal)) return true;
        return false;
    }

    private static List<string?> SplitValueList(string? raw)
    {
        var results = new List<string?>();
        if (string.IsNullOrWhiteSpace(raw)) return results;

        var span = raw.AsSpan().Trim();
        var sb = new StringBuilder();
        char? quote = null;
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (quote is not null)
            {
                if (c == quote)
                {
                    quote = null;
                    continue;
                }
                if (c == '\\' && i + 1 < span.Length)
                {
                    sb.Append(span[i + 1]);
                    i++;
                    continue;
                }
                sb.Append(c);
                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
                continue;
            }

            if (c == ',')
            {
                results.Add(NormalizeValueToken(sb.ToString()));
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
        {
            results.Add(NormalizeValueToken(sb.ToString()));
        }

        return results;
    }

    private static string? NormalizeValueToken(string token)
    {
        var trimmed = token.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool TryConvertList(IReadOnlyList<string?> values, Type targetType, out List<object?> converted, out string? error)
    {
        converted = new List<object?>(values.Count);
        error = null;
        foreach (var value in values)
        {
            if (value is null)
            {
                converted.Add(null);
                continue;
            }

            if (!TryConvertValue(value, targetType, out var typed))
            {
                error = $"Value '{value}' could not be converted to {targetType.Name}.";
                return false;
            }
            converted.Add(typed);
        }
        return true;
    }

    private static bool TryBuildNestedFilterExpression(Type elementType, string rawContent, out LambdaExpression? expression, out string? error)
    {
        expression = null;
        error = null;

        var method = typeof(KyrolusFilterExpressionBuilder)
            .GetMethod(nameof(KyrolusFilterExpressionBuilder.TryBuildFilterExpression), BindingFlags.Public | BindingFlags.Static);

        if (method is null)
        {
            error = "Unable to locate filter expression builder.";
            return false;
        }

        var generic = method.MakeGenericMethod(elementType);
        var args = new object?[] { rawContent, false, null, null };
        var ok = (bool)generic.Invoke(null, args)!;
        expression = args[2] as LambdaExpression;
        error = args[3] as string;
        return ok;
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        private readonly ParameterExpression source = source;
        private readonly ParameterExpression target = target;

        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }

    private static bool TryConvertValue(string raw, Type targetType, out object? value)
    {
        value = null;
        try
        {
            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
            {
                value = null;
                return true;
            }

            if (nonNullable == typeof(string))
            {
                value = raw.Trim('"').Trim('\'');
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

            if (nonNullable == typeof(DateOnly))
            {
                value = DateOnly.Parse(raw, CultureInfo.InvariantCulture);
                return true;
            }

            if (nonNullable == typeof(TimeOnly))
            {
                value = TimeOnly.Parse(raw, CultureInfo.InvariantCulture);
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
