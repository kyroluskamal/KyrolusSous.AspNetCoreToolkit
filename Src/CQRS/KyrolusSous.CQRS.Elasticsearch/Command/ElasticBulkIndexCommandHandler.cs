namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command handler executing <see cref="ElasticBulkIndexCommand{TDocument, TId}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticBulkIndexCommandHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticBulkIndexCommandHandler<TDocument, TId>>? logger = null)
    : IKyrolusCommandHandler<ElasticBulkIndexCommand<TDocument, TId>, KyrolusBulkResult>
    where TDocument : class
{
    public async Task<KyrolusBulkResult> Handle(ElasticBulkIndexCommand<TDocument, TId> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Items);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Executing bulk index for '{DocumentType}'",
            typeof(TDocument).Name);

        return await repository.BulkIndexAsync(command.Items, cancellationToken).ConfigureAwait(false);
    }
}
