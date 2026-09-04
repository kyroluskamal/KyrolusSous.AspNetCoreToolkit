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

        var items = command.Items.ToList();
        if (items.Count > KyrolusElasticBulkLimits.MaxBatchSize)
        {
            // See KyrolusElasticBulkLimits.MaxBatchSize for why this is capped even though Elasticsearch's
            // _bulk API has no hard parameter-count wall. Thrown, not clamped: silently dropping items from
            // a bulk write would be data loss.
            throw new InvalidOperationException(
                $"[Kyrolus CQRS Elasticsearch] Bulk index batch of {items.Count} items exceeds the maximum of " +
                $"{KyrolusElasticBulkLimits.MaxBatchSize}. Split the batch into smaller chunks.");
        }

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Executing bulk index for '{DocumentType}'",
            typeof(TDocument).Name);

        return await repository.BulkIndexAsync(items, cancellationToken).ConfigureAwait(false);
    }
}
