using KyrolusSous.CQRS.Abstractions.Models;
using System.Globalization;

namespace KyrolusSous.CQRS.EF.Query;

public sealed class GetSeekQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusQueryHandler<GetSeekQuery<TResponse, TKey>, KyrolusSeekResult<TResponse>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<KyrolusSeekResult<TResponse>> Handle(GetSeekQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
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
        var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [];
        var totalCount = query.IncludeTotalCount
            ? await ResolveTotalCountAsync(query, orderBy, cancellationToken).ConfigureAwait(false)
            : null;

        List<TResponse> items;
        if (query.IncludeDeleted)
        {
            items = await LoadIncludingDeletedAsync(query, effectiveFilter, orderBy, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
            var spec = new KyrolusEfSeekQuerySpecification<TResponse, TResponse>(
                take: query.PageSize,
                new SpecificationInputs<TResponse, TResponse>(
                    Filter: effectiveFilter,
                    OrderBy: orderBy,
                    Includes: includes,
                    AsNoTracking: query.AsNoTracking ?? false,
                    Selector: query.Selector ?? (static entity => entity),
                    UseSplitQuery: query.UseSplitQuery ?? false,
                    IncludeDeleted: query.IncludeDeleted
                ));
            items = await repo.QueryAsync(spec, cancellationToken).ConfigureAwait(false);
        }

        var nextToken = BuildNextToken(items, seekProperties, query.Descending);
        return new KyrolusSeekResult<TResponse>(items, nextToken, totalCount, query.PageSize);
    }

    private async Task<int?> ResolveTotalCountAsync(
        GetSeekQuery<TResponse, TKey> query,
        Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>> orderBy,
        CancellationToken cancellationToken)
    {
        if (query.IncludeDeleted)
        {
            var soft = TryResolveSoftRepository();
            if (soft is not null)
            {
                var graph = KyrolusIncludeMerge.MergeGraph(query.IncludeGraph, query.IncludeExpressions);
                var items = await soft.GetAllIncludingDeletedAsync(
                    query.Filter,
                    orderBy,
                    query.IncludeProperties,
                    graph,
                    query.AsNoTracking,
                    query.UseSplitQuery,
                    cancellationToken).ConfigureAwait(false);
                return items.Count;
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [];
        var spec = new KyrolusEfPagedQuerySpecification<TResponse>(
            new SpecificationInputs<TResponse, TResponse>(
                Filter: query.Filter,
                OrderBy: orderBy,
                AsNoTracking: true,
                UseSplitQuery: query.UseSplitQuery ?? false,
                IncludeDeleted: query.IncludeDeleted,
                Includes: includes,
                Selector: null
            ),
            pageNumber: 1,
            pageSize: 1);
        var (_, total) = await repo.GetPagedAsync(spec, cancellationToken).ConfigureAwait(false);
        return total;
    }

    private async Task<List<TResponse>> LoadIncludingDeletedAsync(
        GetSeekQuery<TResponse, TKey> query,
        Expression<Func<TResponse, bool>>? filter,
        Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>> orderBy,
        CancellationToken cancellationToken)
    {
        var soft = TryResolveSoftRepository();
        if (soft is null)
        {
            var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
            var spec = new KyrolusEfSeekQuerySpecification<TResponse, TResponse>(
                query.PageSize,
                new SpecificationInputs<TResponse, TResponse>(
                    Filter: query.Filter,
                    OrderBy: orderBy,
                    AsNoTracking: query.AsNoTracking ?? false,
                    UseSplitQuery: query.UseSplitQuery ?? false,
                    Includes: KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [],
                    IncludeDeleted: query.IncludeDeleted,
                    Selector: query.Selector ?? (static entity => entity)
                ));
            return await repo.QueryAsync(spec, cancellationToken).ConfigureAwait(false);
        }

        var graph = KyrolusIncludeMerge.MergeGraph(query.IncludeGraph, query.IncludeExpressions);
        var items = await soft.GetAllIncludingDeletedAsync(
            filter,
            orderBy,
            query.IncludeProperties,
            graph,
            query.AsNoTracking,
            query.UseSplitQuery,
            cancellationToken).ConfigureAwait(false);

        return items.Take(query.PageSize).ToList();
    }

    private IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>? TryResolveSoftRepository()
    {
        try
        {
            return unitOfWork.GetRepository<IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>>();
        }
        catch (InvalidOperationException)
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
            if (!TryBuildCompare(member, typedValue, memberType, descending, out var compareExpr, out error))
            {
                return false;
            }

            var segment = equalsChain is null ? compareExpr : Expression.AndAlso(equalsChain, compareExpr);
            combined = combined is null ? segment : Expression.OrElse(combined, segment);
            equalsChain = equalsChain is null ? equal : Expression.AndAlso(equalsChain, equal);
        }

        predicate = combined is null
            ? Expression.Lambda<Func<TResponse, bool>>(Expression.Constant(false), parameter)
            : Expression.Lambda<Func<TResponse, bool>>(combined, parameter);
        return true;
    }

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
            left = Expression.Property(member, nameof(Nullable<int>.Value));
            right = Expression.Constant(ConvertKeyValue(value, underlying), underlying);
            notNull = Expression.Property(member, nameof(Nullable<int>.HasValue));
            memberType = underlying;
        }
        else if (!memberType.IsValueType)
        {
            notNull = Expression.NotEqual(member, Expression.Constant(null, memberType));
        }
        right ??= Expression.Constant(ConvertKeyValue(value, memberType), memberType);

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

    private static Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>> BuildOrderBy(IReadOnlyList<string> properties, bool descending)
    {
        return query =>
        {
            IOrderedQueryable<TResponse>? ordered = null;
            for (var i = 0; i < properties.Count; i++)
            {
                var prop = properties[i];
                var (segments, memberType) = ResolveMember(prop);
                var parameter = Expression.Parameter(typeof(TResponse), "x");
                var access = BuildMemberAccess(parameter, segments);
                var lambda = Expression.Lambda(access, parameter);
                var isFirst = i == 0;
                var methodName = GetMethodName(isFirst, descending);
                var method = GetQueryableMethod(methodName, typeof(TResponse), memberType);
                var result = method.Invoke(null, [isFirst ? query : ordered!, lambda])!;
                ordered = (IOrderedQueryable<TResponse>)result;
            }

            return ordered!;
        };
    }

    private static (string[] Segments, Type MemberType) ResolveMember(string propertyPath)
    {
        var entityType = typeof(TResponse);
        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        PropertyInfo? property = null;
        Type current = entityType;

        foreach (var segment in segments)
        {
            property = current.GetProperty(segment, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"Property '{propertyPath}' was not found on {entityType.Name}.");
            current = property.PropertyType;
        }

        return (segments, current);
    }

    private static Expression BuildMemberAccess(Expression parameter, string[] segments)
    {
        Expression current = parameter;
        foreach (var segment in segments)
        {
            current = Expression.PropertyOrField(current, segment);
        }
        return current;
    }

    private static string GetMethodName(bool first, bool desc)
    {
        if (first)
        {
            if (desc) return nameof(Queryable.OrderByDescending);
            return nameof(Queryable.OrderBy);
        }

        if (desc) return nameof(Queryable.ThenByDescending);
        return nameof(Queryable.ThenBy);
    }

    private static MethodInfo GetQueryableMethod(string name, Type entityType, Type memberType)
    {
        return typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType, memberType);
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

    private static bool TryGetPropertyValue(object item, string field, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(field)) return false;
        var current = item;
        var currentType = item.GetType();
        foreach (var segment in field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var prop = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null) return false;
            var next = prop.GetValue(current);
            if (next is null)
            {
                value = null;
                return true;
            }
            current = next;
            currentType = next.GetType();
        }

        value = current;
        return true;
    }

    private static Expression<Func<TResponse, bool>>? CombineFilters(Expression<Func<TResponse, bool>>? first, Expression<Func<TResponse, bool>>? second)
    {
        if (first is null) return second;
        if (second is null) return first;
        var parameter = Expression.Parameter(typeof(TResponse), "e");
        var left = new ReplaceParameterVisitor(first.Parameters[0], parameter).Visit(first.Body)!;
        var right = new ReplaceParameterVisitor(second.Parameters[0], parameter).Visit(second.Body)!;
        return Expression.Lambda<Func<TResponse, bool>>(Expression.AndAlso(left, right), parameter);
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

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        private readonly ParameterExpression source = source;
        private readonly ParameterExpression target = target;

        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }
}
