namespace KyrolusSous.Repositories.EF.Abstractions.Pagination;

/// <summary>
/// Represents the result of a keyset (cursor-based) page request.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TCursor">The cursor value type.</typeparam>
public sealed class KyrolusKeysetPageResult<TEntity, TCursor>
    where TEntity : class
    where TCursor : struct, IComparable<TCursor>
{
    /// <summary>
    /// Gets the retrieved items for the current page.
    /// </summary>
    public IReadOnlyList<TEntity> Items { get; init; } = [];

    /// <summary>
    /// Gets whether a subsequent page exists.
    /// </summary>
    public bool HasNextPage { get; init; }

    /// <summary>
    /// Gets whether a preceding page exists.
    /// </summary>
    public bool HasPreviousPage { get; init; }

    /// <summary>
    /// Gets the cursor value to use for fetching the next page.
    /// </summary>
    public TCursor? NextCursor { get; init; }

    /// <summary>
    /// Gets the cursor value to use for fetching the previous page.
    /// </summary>
    public TCursor? PreviousCursor { get; init; }

    /// <summary>
    /// Creates an empty keyset page result.
    /// </summary>
    public static KyrolusKeysetPageResult<TEntity, TCursor> Empty() => new();
}
