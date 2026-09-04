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

        var ids = command.Ids.ToList();
        if (ids.Count > KyrolusElasticBulkLimits.MaxBatchSize)
        {
            // See KyrolusElasticBulkLimits.MaxBatchSize for why this is capped even though Elasticsearch's
            // _bulk API has no hard parameter-count wall. Thrown, not clamped: silently dropping ids from a
            // bulk delete would leave documents behind that the caller believed were removed.
            throw new InvalidOperationException(
                $"[Kyrolus CQRS Elasticsearch] Bulk delete batch of {ids.Count} ids exceeds the maximum of " +
                $"{KyrolusElasticBulkLimits.MaxBatchSize}. Split the batch into smaller chunks.");
        }

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Executing bulk delete for '{DocumentType}'",
            typeof(TDocument).Name);

        return await repository.BulkDeleteAsync(ids, cancellationToken).ConfigureAwait(false);
    }
}
