using System.Globalization;
using System.Reflection;
using System.Text;
using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.Repositories.Marten.Runtime;

public sealed class MartenRuntimeQueryHelper<TEntity> : IQueryHelper<TEntity>
{
    // Keep parsed timestamp values aligned with PostgreSQL microsecond precision.
    private const long TimestampPrecisionTicks = 10L;

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
        if (IsNullOperator(op))
        {
            if (left.Type.IsValueType && Nullable.GetUnderlyingType(left.Type) is null)
            {
                var normalized = NormalizeOperator(op);
                throw new ArgumentException(
                    $"Invalid filter for '{property}': operator '{normalized}' is supported only for nullable or reference types, " +
                    $"but '{property}' is non-nullable '{left.Type.Name}'.");
            }

            return BuildNullCheck(left, NormalizeOperator(op) == "notnull");
        }
        if (value is null)
        {
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

        if (TryBuildNullOperatorPredicate(left, propertyName, opRaw, op, out var nullExpr))
            return nullExpr;

        if (TryBuildSetOrRangePredicate(left, op, rawValue, out var setOrRangeExpr))
            return setOrRangeExpr;

        if (TryBuildAnyAllPredicate(left, op, rawValue, out var anyAllExpr))
            return anyAllExpr;

        var nonNullableType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
        if (nonNullableType == typeof(string))
            return BuildStringPredicate(left, op, rawValue);

        if (!TryConvertToConstant(left, propertyName, opRaw, rawValue, out var constant))
            return null;

        return BuildDefaultPredicateByType(left, propertyName, opRaw, op, nonNullableType, constant);
    }

    private static bool TryBuildNullOperatorPredicate(
    Expression left,
    string propertyName,
    string opRaw,
    string op,
    out Expression? predicate)
    {
        predicate = null;

        if (!IsNullOperator(op))
            return false;

        if (left.Type.IsValueType && Nullable.GetUnderlyingType(left.Type) is null)
            throw new ArgumentException($"Unsupported operator '{opRaw}' for '{propertyName}'");

        predicate = BuildNullCheck(left, op == "notnull");
        return true;
    }

    private static bool TryBuildSetOrRangePredicate(
        Expression left,
        string op,
        string rawValue,
        out Expression? predicate)
    {
        predicate = null;

        if (op == "in")
        {
            var values = SplitValueList(rawValue, '|', ',');
            predicate = TryBuildIn(left, left.Type, values, out var inExpr, out var error) ? inExpr : null;
            if (error is not null)
                throw new ArgumentException(error);
            return true;
        }

        if (op == "between")
        {
            if (TrySplitBetween(rawValue, out var startToken, out var endToken))
            {
                var values = new List<string?> { startToken, endToken };
                predicate = TryBuildBetween(left, left.Type, values, out var betweenExpr, out _) ? betweenExpr : null;
                return true;
            }

            var list = SplitValueList(rawValue, '|', ',');
            predicate = TryBuildBetween(left, left.Type, list, out var betweenExpr2, out _) ? betweenExpr2 : null;
            return true;
        }

        return false;
    }

    private static bool TryBuildAnyAllPredicate(
        Expression left,
        string op,
        string rawValue,
        out Expression? predicate)
    {
        predicate = null;

        if (op is not ("any" or "all"))
            return false;

        var values = SplitValueList(rawValue);
        predicate = TryBuildAnyAll(left, left.Type, values, rawValue, op == "any", out var anyAllExpr, out _)
            ? anyAllExpr
            : null;

        return true;
    }

    private static bool TryConvertToConstant(Expression left, string propertyName,
    string opRaw, string rawValue, out Expression constant)
    {
        constant = null!;

        if (!TryConvertValue(rawValue, left.Type, out var value))
            return false;

        if (value is null)
        {
            if (left.Type.IsValueType && Nullable.GetUnderlyingType(left.Type) is null)
                throw new ArgumentException(
                    $"Invalid filter for '{propertyName}': operator '{opRaw}' cannot use NULL with non-nullable type '{left.Type.Name}'. " +
                    $"Make '{propertyName}' nullable (e.g. {left.Type.Name}?) or provide a non-null value.");
            constant = Expression.Constant(null, left.Type);
            return true;
        }

        // âœ… non-null value
        constant = Expression.Constant(value, value.GetType());

        if (constant.Type != left.Type)
            constant = Expression.Convert(constant, left.Type);

        return true;
    }

    private static Expression? BuildDefaultPredicateByType(
        Expression left,
        string propertyName,
        string opRaw,
        string op,
        Type nonNullableType,
        Expression constant)
    {
        if (IsNumericType(nonNullableType) || IsDateType(nonNullableType))
            return BuildComparablePredicate(left, propertyName, opRaw, op, constant);

        if (nonNullableType == typeof(bool) || nonNullableType == typeof(Guid) || nonNullableType.IsEnum)
            return BuildEqualityOnlyPredicate(left, propertyName, opRaw, op, constant);

        // fallback: equality only for other types
        return IsEqualityOperator(op) ? BuildEquality(op, left, constant) : null;
    }

    private static Expression? BuildComparablePredicate(
        Expression left,
        string propertyName,
        string opRaw,
        string op,
        Expression constant)
    {
        if (IsEqualityOperator(op))
            return BuildEquality(op, left, constant);

        if (TryBuildRelational(op, left, constant, out var predicate))
            return predicate;

        throw new ArgumentException($"Unsupported operator '{opRaw}' for '{propertyName}'");
    }

    private static Expression? BuildEqualityOnlyPredicate(
        Expression left,
        string propertyName,
        string opRaw,
        string op,
        Expression constant)
    {
        if (IsEqualityOperator(op))
            return BuildEquality(op, left, constant);

        throw new ArgumentException($"Unsupported operator '{opRaw}' for '{propertyName}'");
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

    private static bool TryBuildIn(
    Expression member,
    Type memberType,
    IReadOnlyList<string?> values,
    out Expression expression,
    out string? error)
    {
        error = null;
        expression = null!;

        var elementType = Nullable.GetUnderlyingType(memberType) ?? memberType;

        if (!TryConvertList(values, elementType, out var converted, out error))
            return false;

        var hasNull = converted.Any(v => v is null);

        var nonNullConverted = converted.Where(v => v is not null).ToList();

        if (hasNull && Nullable.GetUnderlyingType(memberType) is null && memberType.IsValueType)
        {
            error = $"Operator 'in' does not support NULL for non-nullable type '{memberType.Name}'.";
            return false;
        }

        var list = Array.CreateInstance(elementType, nonNullConverted.Count);
        for (var i = 0; i < nonNullConverted.Count; i++)
            list.SetValue(nonNullConverted[i], i);

        var listExpr = Expression.Constant(list);

        var containsMethod = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        if (memberType != elementType)
        {
            var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
            var value = Expression.Property(member, nameof(Nullable<int>.Value));

            Expression containsValue = Expression.Call(containsMethod, listExpr, value);
            Expression nonNullBranch = Expression.AndAlso(hasValue, containsValue);

            if (hasNull)
            {
                var isNull = Expression.Equal(member, Expression.Constant(null, member.Type));
                expression = Expression.OrElse(isNull, nonNullBranch);
                return true;
            }

            expression = nonNullBranch;
            return true;
        }
        expression = Expression.Call(containsMethod, listExpr, member);
        return true;
    }


    private static bool TryBuildBetween(
    Expression member,
    Type memberType,
    List<string?> values,
    out Expression expression,
    out string? error)
    {
        error = null;
        expression = null!;

        if (values.Count < 2)
        {
            error = "Between requires two values.";
            return false;
        }

        var elementType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (!(IsNumericType(elementType) || IsDateType(elementType) || elementType == typeof(TimeSpan)))
        {
            error = $"Between is not supported for type '{elementType.Name}'.";
            return false;
        }

        if (!TryConvertValue(values[0] ?? string.Empty, elementType, out var start) ||
            !TryConvertValue(values[1] ?? string.Empty, elementType, out var end))
        {
            error = $"Value could not be converted to {elementType.Name}.";
            return false;
        }

        var startConst = Expression.Constant(start, elementType);
        var endConst = Expression.Constant(end, elementType);

        Expression left = member;
        if (memberType != elementType)
            left = Expression.Property(member, nameof(Nullable<>.Value));

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
            return FailAnyAll(isAny, out expression, out error);

        var parameter = Expression.Parameter(elementType, "e");

        if (!TryBuildAnyAllPredicateBody(elementType, values, rawContent, parameter, out var predicateBody, out error))
            return Fail(out expression);

        var predicate = Expression.Lambda(predicateBody!, parameter);
        expression = BuildAnyAllCall(member, memberType, elementType, predicate, isAny);
        return true;
    }

    private static bool Fail(out Expression expression)
    {
        expression = null!;
        return false;
    }

    private static bool FailAnyAll(bool isAny, out Expression expression, out string? error)
    {
        expression = null!;
        error = $"Operator '{(isAny ? "any" : "all")}' is only valid for collection properties.";
        return false;
    }

    private static bool TryBuildAnyAllPredicateBody(
        Type elementType,
        IReadOnlyList<string?> values,
        string? rawContent,
        ParameterExpression parameter,
        out Expression? predicateBody,
        out string? error)
    {
        error = null;
        predicateBody = null;

        if (ShouldUseNestedFilter(rawContent))
            return TryBuildNestedPredicateBody(elementType, rawContent!, parameter, out predicateBody, out error);

        return TryBuildContainsPredicateBody(elementType, values, parameter, out predicateBody, out error);
    }

    private static bool ShouldUseNestedFilter(string? rawContent)
        => !string.IsNullOrWhiteSpace(rawContent) && LooksLikeFilter(rawContent);

    private static bool TryBuildNestedPredicateBody(
        Type elementType,
        string rawContent,
        ParameterExpression parameter,
        out Expression? predicateBody,
        out string? error)
    {
        predicateBody = null;
        error = null;

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
        return true;
    }

    private static bool TryBuildContainsPredicateBody(
        Type elementType,
        IReadOnlyList<string?> values,
        ParameterExpression parameter,
        out Expression? predicateBody,
        out string? error)
    {
        predicateBody = null;
        error = null;

        if (!TryConvertList(values, elementType, out var converted, out error))
            return false;

        var list = Array.CreateInstance(elementType, converted.Count);
        for (var i = 0; i < converted.Count; i++)
            list.SetValue(converted[i], i);

        var listExpr = Expression.Constant(list);

        predicateBody = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            new[] { elementType },
            listExpr,
            parameter);

        return true;
    }

    private static Expression BuildAnyAllCall(
        Expression member,
        Type memberType,
        Type elementType,
        LambdaExpression predicate,
        bool isAny)
    {
        var methodName = isAny ? nameof(Enumerable.Any) : nameof(Enumerable.All);

        var method = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        var call = Expression.Call(method, member, predicate);
        var notNull = Expression.NotEqual(member, Expression.Constant(null, memberType));
        return Expression.AndAlso(notNull, call);
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
        if (span.Contains("..", StringComparison.Ordinal)) return true;
        return false;
    }
    private static bool TrySplitBetween(string raw, out string? start, out string? end)
    {
        start = null;
        end = null;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var span = raw.AsSpan().Trim();
        var sb = new StringBuilder();
        char? quote = null;

        for (int i = 0; i < span.Length; i++)
        {
            var c = span[i];

            // Ø¯Ø§Ø®Ù„ quotes: ØªØ¹Ø§Ù…Ù„ Ù…Ø¹ Ø§Ù„Ø¥ØºÙ„Ø§Ù‚/Ø§Ù„Ù‡Ø±ÙˆØ¨/Ø§Ù„Ø¥Ø¶Ø§ÙØ©
            if (TryHandleQuotedChar(span, ref i, ref quote, sb))
                continue;

            // Ù„Ùˆ Ø¯Ø®Ù„Ù†Ø§ quote Ø¬Ø¯ÙŠØ¯
            if (TryStartQuote(c, ref quote))
                continue;

            // delimiter .. Ø®Ø§Ø±Ø¬ quotes
            if (IsBetweenDelimiter(span, i))
            {
                start = NormalizeValueToken(sb.ToString());
                end = NormalizeValueToken(span.Slice(i + 2).ToString());
                return true;
            }

            sb.Append(c);
        }

        return false;
    }

    private static bool TryHandleQuotedChar(ReadOnlySpan<char> span, ref int i, ref char? quote, StringBuilder sb)
    {
        if (quote is null)
            return false;

        var c = span[i];

        if (c == quote)
        {
            quote = null;
            return true;
        }

        if (c == '\\' && i + 1 < span.Length)
        {
            sb.Append(span[i + 1]);
            i++; // skip escaped char
            return true;
        }

        sb.Append(c);
        return true;
    }

    private static bool TryStartQuote(char c, ref char? quote)
    {
        if (c is not ('\'' or '"'))
            return false;

        quote = c;
        return true;
    }

    private static bool IsBetweenDelimiter(ReadOnlySpan<char> span, int i)
        => span[i] == '.' && i + 1 < span.Length && span[i + 1] == '.';

    private static List<string?> SplitValueList(string? raw, params char[] separators)
    {
        var results = new List<string?>();
        if (string.IsNullOrWhiteSpace(raw))
            return results;

        var sep = NormalizeSeparators(separators);
        var span = raw.AsSpan().Trim();

        var sb = new StringBuilder();
        char? quote = null;

        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];

            if (TryHandleQuotedChar(span, ref i, ref quote, sb))
                continue;

            if (TryStartQuote(c, ref quote))
                continue;

            if (IsSeparator(c, sep))
            {
                AddToken(results, sb);
                continue;
            }

            sb.Append(c);
        }

        AddToken(results, sb);
        return results;
    }


    private static char[] NormalizeSeparators(char[]? separators)
        => (separators is { Length: > 0 }) ? separators : [','];

    private static bool IsSeparator(char c, char[] separators)
    {
        for (int i = 0; i < separators.Length; i++)
            if (separators[i] == c)
                return true;
        return false;
    }

    private static void AddToken(List<string?> results, StringBuilder sb)
    {
        results.Add(NormalizeValueToken(sb.ToString()));
        sb.Clear();
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
                var parsed = DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces);
                value = NormalizeDateTimeOffsetPrecision(parsed);
                return true;
            }

            if (nonNullable == typeof(DateTime))
            {
                var parsed = DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces);
                value = NormalizeDateTimePrecision(parsed);
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

    private static DateTimeOffset NormalizeDateTimeOffsetPrecision(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var ticks = utc.Ticks - (utc.Ticks % TimestampPrecisionTicks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static DateTime NormalizeDateTimePrecision(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        var ticks = utc.Ticks - (utc.Ticks % TimestampPrecisionTicks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}
