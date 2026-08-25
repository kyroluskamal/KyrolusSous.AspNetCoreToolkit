namespace KyrolusSous.CQRS.Abstractions.Models;

/// <summary>
/// Represents a keyset/seek paginated query result.
/// </summary>
public sealed record KyrolusSeekResult<T>(
    IReadOnlyList<T> Items,
    string? NextToken,
    int? TotalCount,
    int PageSize)
{
    /// <summary>
    /// Gets a value indicating whether more pages are available.
    /// </summary>
    public bool HasMore => !string.IsNullOrWhiteSpace(NextToken);
}
