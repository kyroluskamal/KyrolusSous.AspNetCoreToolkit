global using System.Linq.Expressions;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace KyrolusSous.Repositories.EF.Abstractions.Query;

public static class KyrolusFilterExpressionBuilder
{
    public static bool TryBuildFilterExpression<TEntity>(
        string? filter,
        bool caseInsensitive,
        out Expression<Func<TEntity, bool>>? expression,
        out string? error)
    {
        expression = null;
        error = null;
        if (string.IsNullOrWhiteSpace(filter)) return true;

        var parser = new FilterParser<TEntity>(filter, caseInsensitive);
        var body = parser.ParseExpression();
        if (parser.Error is not null)
        {
            error = parser.Error;
            return false;
        }

        if (body is null) return true;
        expression = Expression.Lambda<Func<TEntity, bool>>(body, parser.Parameter);
        return true;
    }

    private sealed class FilterParser<TEntity>
    {
        private readonly string text;
        private readonly bool caseInsensitive;
        private int index;

        public FilterParser(string text, bool caseInsensitive)
        {
            this.text = text;
            this.caseInsensitive = caseInsensitive;
            Parameter = Expression.Parameter(typeof(TEntity), "x");
        }

        public ParameterExpression Parameter { get; }
        public string? Error { get; private set; }

        public Expression? ParseExpression() => ParseOr();

        private Expression? ParseOr()
        {
            var left = ParseAnd();
            while (true)
            {
                SkipWhitespace();
                if (!TryConsume('|')) break;
                var right = ParseAnd();
                if (right is null)
                {
                    Error ??= "Invalid filter expression.";
                    return left;
                }
                left = left is null ? right : Expression.OrElse(left, right);
            }
            return left;
        }

        private Expression? ParseAnd()
        {
            var left = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (!TryConsume(',')) break;
                var right = ParseFactor();
                if (right is null)
                {
                    Error ??= "Invalid filter expression.";
                    return left;
                }
                left = left is null ? right : Expression.AndAlso(left, right);
            }
            return left;
        }

        private Expression? ParseFactor()
        {
            SkipWhitespace();
            if (TryConsume('('))
            {
                var inner = ParseOr();
                SkipWhitespace();
                if (!TryConsume(')'))
                {
                    Error ??= "Missing closing ')'.";
                }
                return inner;
            }

            return ParsePredicate();
        }

        private Expression? ParsePredicate()
        {
            var property = ReadIdentifier();
            if (string.IsNullOrWhiteSpace(property))
            {
                Error ??= "Property name is required.";
                return null;
            }

            SkipWhitespace();
            var op = ReadOperator();
            if (string.IsNullOrWhiteSpace(op))
            {
                Error ??= "Operator is required.";
                return null;
            }

            if (!TryBuildMemberAccess(Parameter, property, out var member, out var memberType, out var memberError))
            {
                Error = memberError;
                return null;
            }

            var normalized = NormalizeOperator(op);
            if (IsUnsupportedOperator(normalized))
            {
                Error ??= $"Operator '{normalized}' is not supported.";
                return null;
            }

            if (IsNullOperator(normalized))
            {
                if (memberType.IsValueType && Nullable.GetUnderlyingType(memberType) is null)
                {
                    Error ??= $"Property '{property}' does not allow null values.";
                    return null;
                }
                return BuildNullCheck(member, normalized == "notnull");
            }

            SkipWhitespace();
            if (!TryReadValue(out var rawValue))
            {
                Error ??= "Value is required.";
                return null;
            }

            if (normalized is "in")
            {
                var values = SplitValueList(rawValue);
                if (!TryBuildIn(member, memberType, values, caseInsensitive, out var inExpr, out var inError))
                {
                    Error = inError;
                    return null;
                }
                return inExpr;
            }

            if (normalized is "between")
            {
                var values = SplitValueList(rawValue);
                if (!TryBuildBetween(member, memberType, values, rawValue, caseInsensitive, out var betweenExpr, out var betweenError))
                {
                    Error = betweenError;
                    return null;
                }
                return betweenExpr;
            }

            if (normalized is "any" or "all")
            {
                var values = SplitValueList(rawValue);
                if (!TryBuildAnyAll(member, memberType, values, rawValue, normalized == "any", caseInsensitive, out var anyAllExpr, out var anyAllError))
                {
                    Error = anyAllError;
                    return null;
                }
                return anyAllExpr;
            }

            if (!TryConvert(rawValue, memberType, out var typedValue))
            {
                Error = $"Value '{rawValue}' could not be converted to {memberType.Name}.";
                return null;
            }

            if (typedValue is null)
            {
                if (!IsNullComparableOperator(normalized))
                {
                    Error ??= $"Operator '{normalized}' does not allow null values.";
                    return null;
                }

                if (memberType.IsValueType && Nullable.GetUnderlyingType(memberType) is null)
                {
                    Error ??= $"Property '{property}' does not allow null values.";
                    return null;
                }

                return BuildNullCheck(member, IsNotEqualsOperator(normalized));
            }

            return BuildComparison(member, memberType, normalized, typedValue, caseInsensitive);
        }

        private string? ReadIdentifier()
        {
            SkipWhitespace();
            if (index >= text.Length) return null;

            var start = index;
            while (index < text.Length)
            {
                var c = text[index];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
                {
                    index++;
                    continue;
                }
                break;
            }

            return start == index ? null : text[start..index];
        }

        private string? ReadOperator()
        {
            SkipWhitespace();
            if (TryMatch("==") || TryMatch("!=") || TryMatch(">=") || TryMatch("<=") || TryMatch("<>"))
            {
                var op = text.Substring(index, 2);
                index += 2;
                return op;
            }

            if (TryMatch("=") || TryMatch(">") || TryMatch("<"))
            {
                var op = text[index].ToString();
                index++;
                return op;
            }

            var word = ReadIdentifier();
            return word;
        }

        private bool TryReadValue(out string? value)
        {
            value = null;
            SkipWhitespace();
            if (index >= text.Length) return false;

            var c = text[index];
            if (c is '\'' or '"')
            {
                value = ReadQuoted();
                return value is not null;
            }

            if (c == '(' || c == '[' || c == '{')
            {
                value = ReadBracketedContent();
                return value is not null;
            }

            var start = index;
            while (index < text.Length)
            {
                c = text[index];
                if (c == ',' || c == '|' || c == ')')
                {
                    break;
                }
                if (char.IsWhiteSpace(c))
                {
                    break;
                }
                index++;
            }

            if (start == index) return false;
            value = text[start..index].Trim();
            return true;
        }

        private string? ReadBracketedContent()
        {
            SkipWhitespace();
            if (index >= text.Length) return null;

            var open = text[index];
            var close = open switch
            {
                '(' => ')',
                '[' => ']',
                '{' => '}',
                _ => '\0'
            };
            if (close == '\0') return null;
            index++;

            var depth = 1;
            var start = index;
            while (index < text.Length)
            {
                var c = text[index];
                if (c == open) depth++;
                if (c == close) depth--;
                if (depth == 0)
                {
                    var content = text[start..index];
                    index++;
                    return content;
                }
                index++;
            }

            Error ??= "Missing closing bracket.";
            return null;
        }

        private string? ReadQuoted()
        {
            if (index >= text.Length) return null;
            var quote = text[index];
            if (quote is not '"' and not '\'') return null;
            index++;

            var sb = new StringBuilder();
            while (index < text.Length)
            {
                var c = text[index++];
                if (c == quote) return sb.ToString();
                if (c == '\\' && index < text.Length)
                {
                    var next = text[index++];
                    sb.Append(next);
                    continue;
                }
                sb.Append(c);
            }

            Error ??= "Missing closing quote.";
            return null;
        }

        private void SkipWhitespace()
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        }

        private bool TryConsume(char token)
        {
            SkipWhitespace();
            if (index < text.Length && text[index] == token)
            {
                index++;
                return true;
            }
            return false;
        }

        private bool TryMatch(string value)
        {
            if (index + value.Length > text.Length) return false;
            return string.Compare(text, index, value, 0, value.Length, StringComparison.Ordinal) == 0;
        }
    }

    private static bool TryBuildMemberAccess(ParameterExpression parameter, string propertyPath, out Expression member, out Type memberType, out string? error)
    {
        error = null;
        member = null!;
        memberType = null!;
        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            error = "Property name is required.";
            return false;
        }

        Expression current = parameter;
        Type currentType = parameter.Type;
        foreach (var segment in segments)
        {
            var property = currentType.GetProperty(segment, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                error = $"Property '{propertyPath}' was not found on {parameter.Type.Name}.";
                return false;
            }

            current = Expression.Property(current, property);
            currentType = property.PropertyType;
        }

        member = current;
        memberType = currentType;
        return true;
    }

    private static Expression BuildComparison(Expression member, Type memberType, string normalizedOperator, object? typedValue, bool caseInsensitive)
    {
        var constant = Expression.Constant(typedValue, memberType);
        var (left, right) = NormalizeStringComparison(member, constant, memberType, caseInsensitive);

        return normalizedOperator switch
        {
            "==" or "eq" => Expression.Equal(left, right),
            "!=" or "neq" => Expression.NotEqual(left, right),
            ">" or "gt" => Expression.GreaterThan(left, right),
            "<" or "lt" => Expression.LessThan(left, right),
            ">=" or "gte" => Expression.GreaterThanOrEqual(left, right),
            "<=" or "lte" => Expression.LessThanOrEqual(left, right),
            "contains" => BuildStringCall(left, nameof(string.Contains), right),
            "startswith" => BuildStringCall(left, nameof(string.StartsWith), right),
            "endswith" => BuildStringCall(left, nameof(string.EndsWith), right),
            _ => throw new ArgumentException($"Unsupported operator '{normalizedOperator}'")
        };
    }

    private static bool TryBuildIn(Expression member, Type memberType, IReadOnlyList<string?> values, bool caseInsensitive, out Expression expression, out string? error)
    {
        error = null;
        expression = null!;
        var elementType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (!TryConvertList(values, elementType, out var converted, out error))
        {
            return false;
        }

        var hasNull = converted.Any(v => v is null);

        if (hasNull && Nullable.GetUnderlyingType(memberType) is null && memberType.IsValueType)
        {
            error = $"Operator 'in' does not support NULL for non-nullable type '{memberType.Name}'.";
            return false;
        }

        if (memberType == typeof(string) && caseInsensitive)
        {
            var lowered = converted.Select(v => v?.ToString()?.ToLowerInvariant()).ToArray();
            var listConst = Expression.Constant(lowered);
            var memberLower = Expression.Call(member, nameof(string.ToLower), Type.EmptyTypes);
            var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)], listConst, memberLower);
            expression = Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, typeof(string))), contains);
            return true;
        }

        var nonNullConverted = converted.Where(v => v is not null).ToList();
        var list = Array.CreateInstance(elementType, nonNullConverted.Count);
        for (var i = 0; i < nonNullConverted.Count; i++)
        {
            list.SetValue(nonNullConverted[i], i);
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
            var nonNullBranch = Expression.AndAlso(hasValue, containsValue);
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

    private static bool TryBuildBetween(Expression member, Type memberType, IReadOnlyList<string?> values, string? rawValue, bool caseInsensitive, out Expression expression, out string? error)
    {
        error = null;
        expression = null!;
        if (values.Count < 2)
        {
            if (TrySplitBetween(rawValue, out var startToken, out var endToken))
            {
                values = new List<string?> { startToken, endToken };
            }
            else
            {
                error = "Between requires two values.";
                return false;
            }
        }

        var elementType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (!TryConvert(values[0], elementType, out var start) || !TryConvert(values[1], elementType, out var end))
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

        var (normalizedLeft, normalizedStart) = NormalizeStringComparison(left, startConst, elementType, caseInsensitive);
        var (_, normalizedEnd) = NormalizeStringComparison(left, endConst, elementType, caseInsensitive);
        var ge = Expression.GreaterThanOrEqual(normalizedLeft, normalizedStart);
        var le = Expression.LessThanOrEqual(normalizedLeft, normalizedEnd);
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
        bool caseInsensitive,
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
            if (!TryBuildFilterExpressionInternal(elementType, rawContent, caseInsensitive, out var nested, out var nestedError))
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

            if (elementType == typeof(string) && caseInsensitive)
            {
                var lowered = converted.Select(v => v?.ToString()?.ToLowerInvariant()).ToArray();
                var listConst = Expression.Constant(lowered);
                var elementLower = Expression.Call(parameter, nameof(string.ToLower), Type.EmptyTypes);
                predicateBody = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)], listConst, elementLower);
            }
            else
            {
                var list = Array.CreateInstance(elementType, converted.Count);
                for (var i = 0; i < converted.Count; i++)
                {
                    list.SetValue(converted[i], i);
                }
                var listExpr = Expression.Constant(list);
                predicateBody = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [elementType], listExpr, parameter);
            }
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

    private static (Expression Left, Expression Right) NormalizeStringComparison(Expression member, Expression constant, Type memberType, bool caseInsensitive)
    {
        if (!caseInsensitive || memberType != typeof(string)) return (member, constant);

        var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var memberLower = Expression.Call(member, toLower);
        var constantLower = Expression.Call(constant, toLower);
        return (memberLower, constantLower);
    }

    private static Expression BuildNullCheck(Expression member, bool notNull)
    {
        var nullConstant = Expression.Constant(null, member.Type);
        return notNull ? Expression.NotEqual(member, nullConstant) : Expression.Equal(member, nullConstant);
    }

    private static Expression BuildStringCall(Expression member, string methodName, Expression constant)
    {
        if (member.Type != typeof(string))
        {
            throw new ArgumentException($"Operator '{methodName}' is only valid for string properties.");
        }

        var call = Expression.Call(member, typeof(string).GetMethod(methodName, [typeof(string)])!, constant);
        return Expression.Equal(call, Expression.Constant(true));
    }

    private static string NormalizeOperator(string op)
    {
        var normalized = op.Trim().ToLowerInvariant();
        return normalized switch
        {
            "eq" => "==",
            "neq" => "!=",
            "=" => "==",
            "<>" => "!=",
            "gt" => ">",
            "lt" => "<",
            "gte" => ">=",
            "lte" => "<=",
            _ => normalized
        };
    }

    private static bool IsUnsupportedOperator(string normalized)
        => normalized is not (
            "==" or "!=" or ">" or "<" or ">=" or "<="
            or "eq" or "neq" or "gt" or "lt" or "gte" or "lte"
            or "contains" or "startswith" or "endswith"
            or "in" or "between" or "any" or "all"
            or "isnull" or "notnull");

    private static bool IsNullComparableOperator(string normalized)
        => normalized is "==" or "!=" or "eq" or "neq";

    private static bool IsNotEqualsOperator(string normalized)
        => normalized is "!=" or "neq";

    private static bool IsNullOperator(string normalized)
        => normalized is "isnull" or "notnull";

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

            if (c == ',' || c == '|')
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

    private static bool TrySplitBetween(string? raw, out string? start, out string? end)
    {
        start = null;
        end = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var span = raw.AsSpan().Trim();
        var sb = new StringBuilder();
        char? quote = null;

        for (int i = 0; i < span.Length; i++)
        {
            var c = span[i];

            if (TryHandleQuotedChar(span, ref i, ref quote, sb))
                continue;

            if (TryStartQuote(c, ref quote))
                continue;

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
            i++;
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
            if (!TryConvert(value, targetType, out var typed))
            {
                error = $"Value '{value}' could not be converted to {targetType.Name}.";
                return false;
            }
            converted.Add(typed);
        }
        return true;
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
            result = raw.Trim('"').Trim('\'');
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
            result = Convert.ChangeType(raw, nonNullableType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBuildFilterExpressionInternal(
        Type entityType,
        string? filter,
        bool caseInsensitive,
        out LambdaExpression? expression,
        out string? error)
    {
        expression = null;
        error = null;
        if (string.IsNullOrWhiteSpace(filter)) return true;

        var generic = typeof(KyrolusFilterExpressionBuilder)
            .GetMethod(nameof(TryBuildFilterExpression), BindingFlags.Public | BindingFlags.Static);

        if (generic is null)
        {
            error = "Unable to locate filter expression builder.";
            return false;
        }

        var concrete = generic.MakeGenericMethod(entityType);
        var args = new object?[] { filter, caseInsensitive, null, null };
        var result = (bool)concrete.Invoke(null, args)!;
        expression = args[2] as LambdaExpression;
        error = args[3] as string;
        return result;
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        private readonly ParameterExpression source = source;
        private readonly ParameterExpression target = target;

        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }
}
