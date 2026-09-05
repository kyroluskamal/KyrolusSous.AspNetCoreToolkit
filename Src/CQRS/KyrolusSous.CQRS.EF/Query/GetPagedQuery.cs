using KyrolusSous.CQRS.Abstractions.Models;

namespace KyrolusSous.CQRS.EF.Query;

public sealed class GetPagedQuery<TResponse, TKey>(int pageNumber, int pageSize, bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusQuery<KyrolusPagedResult<TResponse>>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public Expression<Func<TResponse, bool>>? Filter { get; set; }
    public Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? OrderBy { get; set; }
    public List<string>? IncludeProperties { get; set; }
    public Expression<Func<TResponse, object?>>[]? IncludeExpressions { get; set; }
    public IncludeGraph<TResponse>? IncludeGraph { get; set; }
    public bool? AsNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public int PageNumber { get; set; } = pageNumber;
    public int PageSize { get; set; } = pageSize;
    public Expression<Func<TResponse, TResponse>>? Selector { get; set; }

    /// <remarks>
    /// Mirrors <see cref="GetAllQuery{TResponse}.IncludeDeleted"/> and
    /// <see cref="GetSeekQuery{TResponse, TKey}.IncludeDeleted"/>: routes through the soft-delete
    /// repository (when one is registered) so soft-deleted rows appear on the page instead of being
    /// silently excluded. Defaults to <see langword="false"/> to preserve pre-existing behavior.
    /// </remarks>
    public bool IncludeDeleted { get; set; }

    /// <remarks>Mirrors <see cref="GetAllQuery{TResponse}.DeletedOnly"/>. Defaults to <see langword="false"/>.</remarks>
    public bool DeletedOnly { get; set; }
}
