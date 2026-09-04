using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.Repositories.Marten.Abstractions.Query;
using System.Globalization;

namespace KyrolusSous.CQRS.Marten.Query;

public sealed class GetSeekQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusQueryHandler<GetSeekQuery<TResponse, TKey>, KyrolusSeekResult<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<KyrolusSeekResult<TResponse>> Handle(GetSeekQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        // Clamp caller-supplied PageSize so int.MaxValue (or negative) can't force the database to
        // attempt to materialize an enormous or malformed result set. Mutated in place so every
        // downstream Take(query.PageSize) call below sees the clamped value.
        query.PageSize = Math.Clamp(query.PageSize, 1, KyrolusPagingLimits.MaxPageSize);

        var seekProperties = query.SeekPropertyNames?.Where(static p => !string.IsNullOrWhiteSpace(p)).ToArray();
        if (seekProperties is null || seekProperties.Length == 0)
        {
            throw new InvalidOperationException("SeekPropertyNames is required.");
        }

        Expression<Func<TResponse, bool>>? cursorFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            if (!KyrolusSeekToken.TryDecode(query.Cursor, out var payload))
            {
                throw new InvalidOperationException("Invalid cursor token.");
            }

            if (!TryBuildSeekPredicate(seekProperties, payload.Keys, query.Descending, out cursorFilter, out var error))
            {
                throw new InvalidOperationException(error ?? "Invalid cursor token.");
            }
        }

        var effectiveFilter = CombineFilters(query.Filter, cursorFilter);
        var orderBy = BuildOrderBy(seekProperties, query.Descending);
        var options = BuildOptions(query, effectiveFilter, orderBy);
        // Count against query.Filter alone, not effectiveFilter - effectiveFilter also carries the
        // cursor predicate ("rows after the last seen row"), which would make TotalCount shrink on
        // every subsequent page instead of staying constant across the whole seek. Mirrors the EF
        // provider's GetSeekQueryHandler.ResolveTotalCountAsync.
        var total = query.IncludeTotalCount
            ? await ResolveTotalCountAsync(query, BuildOptions(query, query.Filter, orderBy), cancellationToken).ConfigureAwait(false)
            : null;

        IEnumerable<TResponse> items;
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        if (query.Selector is not null)
        {
            items = await repo.QueryAsync<TResponse>(options, q => (global::Marten.Linq.IMartenQueryable<TResponse>)q.Select(query.Selector).Take(query.PageSize), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            items = await repo.QueryAsync<TResponse>(options, q => (global::Marten.Linq.IMartenQueryable<TResponse>)q.Take(query.PageSize), cancellationToken).ConfigureAwait(false);
        }

        var list = items.ToList();
        var nextToken = BuildNextToken(list, seekProperties, query.Descending);
        // TotalCount is nullable (only populated when IncludeTotalCount is requested) — pass it
        // through as-is instead of force-unwrapping, which previously threw whenever the caller
        // left IncludeTotalCount at its default of false (i.e. on every ordinary seek call).
        return new KyrolusSeekResult<TResponse>(list, nextToken, (int?)total, query.PageSize);
    }

    private async Task<long?> ResolveTotalCountAsync(GetSeekQuery<TResponse, TKey> query, MartenQueryOptions<TResponse> options, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        if (query.IncludeDeleted)
        {
            var soft = TryResolveSoftRepository();
            if (soft is not null)
            {
                var all = await soft.GetAllIncludingDeletedAsync(options, cancellationToken).ConfigureAwait(false);
                return all.Count();
            }
        }

        var page = await repo.GetPageAsync(options, new MartenPageRequest(1, 1), cancellationToken).ConfigureAwait(false);
        return page.TotalCount;
    }

    private static MartenQueryOptions<TResponse> BuildOptions(
        GetSeekQuery<TResponse, TKey> query,
        Expression<Func<TResponse, bool>>? filter,
        Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>> orderBy)
    {
        var mergedExpressions = MergeIncludeExpressions(query.IncludeExpressions, query.IncludeGraph);
        return new MartenQueryOptions<TResponse>(
            Filter: filter,
            OrderBy: orderBy,
            IncludeProperties: query.IncludeProperties,
            IncludeExpressions: mergedExpressions,
            TenantId: query.TenantId,
            IncludeSoftDeleted: query.IncludeDeleted);
    }

    private static Expression<Func<TResponse, object?>>[]? MergeIncludeExpressions(
        Expression<Func<TResponse, object?>>[]? includes,
        IncludeGraph<TResponse>? graph)
    {
        if (includes is null && (graph?.Includes?.Count ?? 0) == 0) return null;
        var merged = new List<Expression<Func<TResponse, object?>>>();
        if (includes is not null) merged.AddRange(includes);
        if (graph?.Includes is not null) merged.AddRange(graph.Includes);
        return merged.Count == 0 ? null : merged.ToArray();
    }

    private IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>? TryResolveSoftRepository()
    {
        try
        {
            return unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>>();
        }
        catch (InvalidOperationException ex) when (ex.IsRepositoryNotRegistered())
        {
            return null;
        }
    }

    private static string? BuildNextToken(IReadOnlyList<TResponse> items, IReadOnlyList<string> properties, bool descending)
    {
        if (items.Count == 0) return null;
        var last = items[^1];
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in properties)
        {
            if (!TryGetPropertyValue(last, prop, out var value))
            {
                return null;
            }
            values[prop] = value;
        }

        return KyrolusSeekToken.Encode(values, descending);
    }

    private static Expression<Func<TResponse, bool>>? CombineFilters(
        Expression<Func<TResponse, bool>>? left,
        Expression<Func<TResponse, bool>>? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        var param = Expression.Parameter(typeof(TResponse), "e");
        var leftBody = new ReplaceParameterVisitor(left.Parameters[0], param).Visit(left.Body);
        var rightBody = new ReplaceParameterVisitor(right.Parameters[0], param).Visit(right.Body);
        var body = Expression.AndAlso(leftBody!, rightBody!);
        return Expression.Lambda<Func<TResponse, bool>>(body, param);
    }

    private static Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>> BuildOrderBy(
        IReadOnlyList<string> properties,
        bool descending)
    {
        return query =>
        {
            IOrderedQueryable<TResponse>? ordered = null;
            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var parameter = Expression.Parameter(typeof(TResponse), "e");
                var member = BuildMemberAccess(parameter, property);
                var lambda = Expression.Lambda(member, parameter);
                var methodName = GetOrderMethodName(i == 0, descending);
                var method = GetQueryableMethod(methodName, typeof(TResponse), member.Type);
                var target = i == 0 ? query : ordered!;
                ordered = (IOrderedQueryable<TResponse>)method.Invoke(null, [target, lambda])!;
            }
            return ordered!;
        };
    }

    private static bool TryBuildSeekPredicate(
        IReadOnlyList<string> properties,
        IReadOnlyDictionary<string, string?> values,
        bool descending,
        out Expression<Func<TResponse, bool>>? predicate,
        out string? error)
    {
        error = null;
        predicate = null;
        var parameter = Expression.Parameter(typeof(TResponse), "e");
        Expression? combined = null;
        Expression? equalsChain = null;

        foreach (var prop in properties)
        {
            if (!TryBuildMemberAccess(parameter, prop, out var member, out var memberType, out error))
            {
                return false;
            }

            if (!values.TryGetValue(prop, out var raw))
            {
                error = $"Cursor value for '{prop}' is missing.";
                return false;
            }

            if (!TryConvert(raw, memberType, out var typedValue))
            {
                error = $"Cursor value for '{prop}' is invalid.";
                return false;
            }

            var constant = Expression.Constant(typedValue, memberType);
            var equal = Expression.Equal(member, constant);
            if (!TryBuildCompare(member, typedValue!, memberType, descending, out var compareExpr, out error))
            {
                return false;
            }

            if (equalsChain is null)
            {
                combined = compareExpr;
                equalsChain = equal;
            }
            else
            {
                var next = Expression.AndAlso(equalsChain, compareExpr);
                combined = Expression.OrElse(combined!, next);
                equalsChain = Expression.AndAlso(equalsChain, equal);
            }
        }

        if (combined is null)
        {
            return true;
        }

        predicate = Expression.Lambda<Func<TResponse, bool>>(combined, parameter);
        return true;
    }

    private static bool TryBuildMemberAccess(
        ParameterExpression parameter,
        string propertyPath,
        out Expression member,
        out Type memberType,
        out string? error)
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
            var prop = currentType.GetProperty(segment, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop is null)
            {
                error = $"Property '{propertyPath}' was not found on {parameter.Type.Name}.";
                return false;
            }

            current = Expression.Property(current, prop);
            currentType = prop.PropertyType;
        }

        member = current;
        memberType = currentType;
        return true;
    }

    private static Expression BuildMemberAccess(Expression parameter, string propertyPath)
    {
        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Expression current = parameter;
        foreach (var segment in segments)
        {
            current = Expression.PropertyOrField(current, segment);
        }
        return current;
    }

    /// <summary>
    /// Builds the seek continuation comparison via <see cref="IComparable.CompareTo"/> (found by
    /// reflection) rather than <c>Expression.GreaterThan</c>/<c>LessThan</c>, which throw
    /// <see cref="InvalidOperationException"/> for types with no built-in comparison operator
    /// (e.g. <see cref="string"/>, <see cref="Guid"/>) — both common seek-key types. Mirrors the
    /// EF provider's <c>GetSeekQueryHandler.TryBuildCompare</c>.
    /// </summary>
    private static bool TryBuildCompare(Expression member, object? value, Type memberType, bool descending, out Expression comparison, out string? error)
    {
        error = null;
        comparison = null!;
        Expression left = member;
        Expression? right = null;
        Expression? notNull = null;

        var underlying = Nullable.GetUnderlyingType(memberType);
        if (underlying is not null)
        {
            notNull = Expression.Property(member, nameof(Nullable<int>.HasValue));
            var convertedValue = ConvertKeyValue(value, underlying);
            if (convertedValue is null)
            {
                // Last-seen value for this column was null. Under nulls-first default ordering,
                // anything non-null sorts after null, and nothing sorts before it.
                comparison = descending ? Expression.Constant(false) : notNull;
                return true;
            }

            left = Expression.Property(member, nameof(Nullable<int>.Value));
            right = Expression.Constant(convertedValue, underlying);
            memberType = underlying;
        }
        else if (!memberType.IsValueType)
        {
            notNull = Expression.NotEqual(member, Expression.Constant(null, memberType));
        }
        right ??= Expression.Constant(ConvertKeyValue(value, memberType), memberType);

        if (memberType.IsEnum)
        {
            // Enum doesn't implement IComparable<TEnum> - GetMethod("CompareTo", [enumType]) below
            // resolves to the inherited Enum.CompareTo(object), and Expression.Call then rejects an
            // enum-typed (not object-typed) argument for it. Compare on the underlying integral type
            // instead, which has its own real CompareTo(self).
            var enumUnderlying = Enum.GetUnderlyingType(memberType);
            left = Expression.Convert(left, enumUnderlying);
            right = Expression.Convert(right, enumUnderlying);
            memberType = enumUnderlying;
        }

        var compareMethod = memberType.GetMethod("CompareTo", new[] { memberType });
        if (compareMethod is null)
        {
            error = $"Type '{memberType.Name}' does not support ordering.";
            return false;
        }

        var compareCall = Expression.Call(left, compareMethod, right);
        var zero = Expression.Constant(0);
        comparison = descending
            ? Expression.LessThan(compareCall, zero)
            : Expression.GreaterThan(compareCall, zero);

        if (notNull is not null)
        {
            comparison = Expression.AndAlso(notNull, comparison);
        }

        return true;
    }

    private static object? ConvertKeyValue(object? value, Type targetType)
    {
        if (value is null) return null;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value)) return value;
        if (value is string s)
        {
            if (underlying == typeof(Guid) && Guid.TryParse(s, out var guid)) return guid;
            if (underlying == typeof(DateTimeOffset) && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)) return dto;
            if (underlying == typeof(DateTime) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)) return dt;
            if (underlying.IsEnum) return Enum.Parse(underlying, s, ignoreCase: true);
        }
        if (value is IConvertible) return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return value;
    }

    private static string GetOrderMethodName(bool first, bool descending)
    {
        if (first)
        {
            if (descending) return nameof(Queryable.OrderByDescending);
            return nameof(Queryable.OrderBy);
        }

        if (descending) return nameof(Queryable.ThenByDescending);
        return nameof(Queryable.ThenBy);
    }

    private static MethodInfo GetQueryableMethod(string name, Type entityType, Type memberType)
    {
        return typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType, memberType);
    }

    private static bool TryGetPropertyValue(TResponse entity, string propertyPath, out object? value)
    {
        value = null;
        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        object? current = entity;
        foreach (var segment in segments)
        {
            if (current is null) return false;
            var prop = current.GetType().GetProperty(segment, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop is null) return false;
            current = prop.GetValue(current);
        }
        value = current;
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

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        private readonly ParameterExpression source = source;
        private readonly ParameterExpression target = target;

        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }
}

