namespace KyrolusSous.Repositories.EF.Abstractions.Pagination;

/// <summary>
/// Defines a strongly-typed keyset (cursor-based) pagination specification for $O(1)$ constant-time seek queries.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TCursor">The cursor value type (e.g. <see cref="long"/>, <see cref="int"/>, or <see cref="DateTime"/>).</typeparam>
public interface IKyrolusKeysetSpecification<TEntity, TCursor>
    where TEntity : class
    where TCursor : struct, IComparable<TCursor>
{
    /// <summary>
    /// Gets the cursor selector expression.
    /// </summary>
    Expression<Func<TEntity, TCursor>> CursorSelector { get; }

    /// <summary>
    /// Gets the current cursor reference value to seek from (or <c>null</c> for first page).
    /// </summary>
    TCursor? CursorValue { get; }

    /// <summary>
    /// Gets the seek direction (forward or backward).
    /// </summary>
    KyrolusKeysetDirection Direction { get; }

    /// <summary>
    /// Gets the maximum number of items to retrieve in the page.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// Gets optional additional filtering criteria.
    /// </summary>
    Expression<Func<TEntity, bool>>? Filter { get; }

    /// <summary>
    /// Gets whether sorting is descending by cursor.
    /// </summary>
    bool IsDescending { get; }
}
