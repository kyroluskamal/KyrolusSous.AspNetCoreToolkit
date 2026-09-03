namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command handler executing <see cref="ElasticDeleteDocumentCommand{TDocument, TId}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticDeleteDocumentCommandHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticDeleteDocumentCommandHandler<TDocument, TId>>? logger = null)
    : IKyrolusCommandHandler<ElasticDeleteDocumentCommand<TDocument, TId>, bool>
    where TDocument : class
{
    public async Task<bool> Handle(ElasticDeleteDocumentCommand<TDocument, TId> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Deleting '{DocumentType}' document with Id '{Id}'",
            typeof(TDocument).Name,
            command.Id);

        return await repository.DeleteAsync(command.Id, cancellationToken).ConfigureAwait(false);
    }
}
