namespace KyrolusSous.CQRS.Marten.Query;

/// <summary>
/// Shared paging safety limits for Marten paged/seek queries. Without a clamp, a caller could
/// request <see cref="int.MaxValue"/> (or a negative) PageSize/PageNumber and force the database
/// to attempt to materialize an enormous or malformed result set.
/// </summary>
public static class KyrolusPagingLimits
{
    /// <summary>
    /// Maximum PageSize accepted by <c>GetPagedQuery</c>/<c>GetSeekQuery</c> handlers. Requests
    /// above this are clamped down rather than rejected.
    /// </summary>
    public const int MaxPageSize = 200;
}
