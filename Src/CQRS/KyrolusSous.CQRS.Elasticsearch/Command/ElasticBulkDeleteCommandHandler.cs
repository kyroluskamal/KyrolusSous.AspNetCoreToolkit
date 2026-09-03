namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command handler executing <see cref="ElasticBulkDeleteCommand{TDocument, TId}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticBulkDeleteCommandHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticBulkDeleteCommandHandler<TDocument, TId>>? logger = null)
    : IKyrolusCommandHandler<ElasticBulkDeleteCommand<TDocument, TId>, KyrolusBulkResult>
    where TDocument : class
{
    public async Task<KyrolusBulkResult> Handle(ElasticBulkDeleteCommand<TDocument, TId> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Ids);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Executing bulk delete for '{DocumentType}'",
            typeof(TDocument).Name);

        return await repository.BulkDeleteAsync(command.Ids, cancellationToken).ConfigureAwait(false);
    }
}
