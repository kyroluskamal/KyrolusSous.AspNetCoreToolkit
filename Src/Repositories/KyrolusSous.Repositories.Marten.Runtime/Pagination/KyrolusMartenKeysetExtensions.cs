using System.Linq.Expressions;
using KyrolusSous.Repositories.Marten.Abstractions.Pagination;

namespace KyrolusSous.Repositories.Marten.Runtime.Pagination;

/// <summary>
/// Provides keyset/cursor-based pagination for high-performance O(1) Marten document queries.
/// </summary>
public static class KyrolusMartenKeysetExtensions
{
    /// <summary>
    /// Executes a keyset pagination query in-memory or on Marten <see cref="IQueryable{TDoc}"/>.
    /// </summary>
    public static MartenKeysetPage<TDoc, TKey?> ToMartenKeysetPage<TDoc, TKey>(
        this IQueryable<TDoc> query,
        Expression<Func<TDoc, TKey>> keySelector,
        TKey? cursor,
        int pageSize,
        bool descending = false)
        where TDoc : class
        where TKey : struct, IComparable<TKey>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be greater than zero.");
        }

        var param = keySelector.Parameters[0];
        var keyProperty = keySelector.Body;

        IQueryable<TDoc> filteredQuery = query;

        if (cursor.HasValue)
        {
            var cursorConst = Expression.Constant(cursor.Value, typeof(TKey));
            Expression comparison = descending
                ? Expression.LessThan(keyProperty, cursorConst)
                : Expression.GreaterThan(keyProperty, cursorConst);

            var filterLambda = Expression.Lambda<Func<TDoc, bool>>(comparison, param);
            filteredQuery = filteredQuery.Where(filterLambda);
        }

        filteredQuery = descending
            ? filteredQuery.OrderByDescending(keySelector)
            : filteredQuery.OrderBy(keySelector);

        var fetched = filteredQuery.Take(pageSize + 1).ToList();
        var hasNext = fetched.Count > pageSize;
        var items = hasNext ? fetched.Take(pageSize).ToList() : fetched;

        var keyFunc = keySelector.Compile();
        TKey? nextCursor = hasNext && items.Count > 0
            ? keyFunc(items[^1])
            : null;

        return new MartenKeysetPage<TDoc, TKey?>(items, hasNext, nextCursor);
    }
}
