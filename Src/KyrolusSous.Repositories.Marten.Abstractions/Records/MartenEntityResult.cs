namespace KyrolusSous.Repositories.Marten.Abstractions.Records;

public sealed record MartenEntityResult<TEntity>(TEntity? Entity, Guid? Version);
