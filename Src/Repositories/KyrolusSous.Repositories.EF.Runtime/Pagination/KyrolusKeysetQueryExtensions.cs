using KyrolusSous.Repositories.EF.Abstractions.Pagination;

namespace KyrolusSous.Repositories.EF.Runtime.Pagination;

/// <summary>
/// Provides high-speed keyset (cursor-based) pagination query extensions on <see cref="IQueryable{TEntity}"/>.
/// </summary>
public static class KyrolusKeysetQueryExtensions
{
    /// <summary>
    /// Executes an $O(1)$ constant-time keyset seek query against the database using a strongly-typed cursor specification.
    /// </summary>
    public static async Task<KyrolusKeysetPageResult<TEntity, TCursor>> ToKeysetPageAsync<TEntity, TCursor>(
        this IQueryable<TEntity> source,
        IKyrolusKeysetSpecification<TEntity, TCursor> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TCursor : struct, IComparable<TCursor>
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        var query = source;

        if (specification.Filter is not null)
        {
            query = query.Where(specification.Filter);
        }

        var cursorSelector = specification.CursorSelector;

        // Apply cursor predicate if reference cursor value is present
        if (specification.CursorValue.HasValue)
        {
            var cursorVal = specification.CursorValue.Value;
            var param = cursorSelector.Parameters[0];
            var propertyAccess = cursorSelector.Body;
            var constant = Expression.Constant(cursorVal, typeof(TCursor));

            // Forward + Ascending  -> >
            // Forward + Descending -> <
            // Backward + Ascending -> <
            // Backward + Descending -> >
            var isGreaterThan = (specification.Direction == KyrolusKeysetDirection.Forward && !specification.IsDescending) ||
                                (specification.Direction == KyrolusKeysetDirection.Backward && specification.IsDescending);

            var comparison = isGreaterThan
                ? Expression.GreaterThan(propertyAccess, constant)
                : Expression.LessThan(propertyAccess, constant);

            var predicate = Expression.Lambda<Func<TEntity, bool>>(comparison, param);
            query = query.Where(predicate);
        }

        // Apply ordering based on cursor selector and direction
        var isOrderDesc = specification.Direction == KyrolusKeysetDirection.Backward
            ? !specification.IsDescending
            : specification.IsDescending;

        query = isOrderDesc
            ? query.OrderByDescending(cursorSelector)
            : query.OrderBy(cursorSelector);

        // Fetch PageSize + 1 to determine if subsequent page exists
        var takeCount = specification.PageSize + 1;
        var rawItems = await query.Take(takeCount).ToListAsync(cancellationToken).ConfigureAwait(false);

        var hasMore = rawItems.Count > specification.PageSize;
        var items = hasMore ? rawItems.Take(specification.PageSize).ToList() : rawItems;

        // If backward navigation was requested, reverse back to natural order
        if (specification.Direction == KyrolusKeysetDirection.Backward)
        {
            items.Reverse();
        }

        var compiledSelector = cursorSelector.Compile();
        var nextCursor = items.Count > 0 ? (TCursor?)compiledSelector(items[^1]) : null;
        var prevCursor = items.Count > 0 ? (TCursor?)compiledSelector(items[0]) : null;

        return new KyrolusKeysetPageResult<TEntity, TCursor>
        {
            Items = items,
            HasNextPage = specification.Direction == KyrolusKeysetDirection.Forward ? hasMore : specification.CursorValue.HasValue,
            HasPreviousPage = specification.Direction == KyrolusKeysetDirection.Backward ? hasMore : specification.CursorValue.HasValue,
            NextCursor = nextCursor,
            PreviousCursor = prevCursor
        };
    }
}
