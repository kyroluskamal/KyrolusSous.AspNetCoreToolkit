namespace KyrolusSous.Repositories.Marten.Abstractions.Records;

public sealed record MartenPageRequest(int PageNumber = 1, int PageSize = 20);
