namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command handler executing <see cref="ElasticIndexDocumentCommand{TDocument, TId}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticIndexDocumentCommandHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticIndexDocumentCommandHandler<TDocument, TId>>? logger = null)
    : IKyrolusCommandHandler<ElasticIndexDocumentCommand<TDocument, TId>, bool>
    where TDocument : class
{
    public async Task<bool> Handle(ElasticIndexDocumentCommand<TDocument, TId> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Document);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Indexing '{DocumentType}' document with Id '{Id}'",
            typeof(TDocument).Name,
            command.Id);

        return await repository.AddAsync(command.Document, command.Id, cancellationToken).ConfigureAwait(false);
    }
}
