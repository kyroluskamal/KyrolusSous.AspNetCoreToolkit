namespace KyrolusSous.CQRS.Abstractions.Models;

public sealed record KyrolusSeekResult<T>(
    IReadOnlyList<T> Items,
    string? NextToken,
    int? TotalCount,
    int PageSize);
