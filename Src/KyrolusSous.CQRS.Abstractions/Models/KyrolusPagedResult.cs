namespace KyrolusSous.CQRS.Abstractions.Models;

public sealed record KyrolusPagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
