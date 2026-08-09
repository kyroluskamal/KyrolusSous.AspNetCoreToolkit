namespace KyrolusSous.Repositories.Marten.Abstractions.Records;

public sealed record PageResult<T>(IReadOnlyList<T> Items, long TotalCount, int PageNumber, int PageSize);
