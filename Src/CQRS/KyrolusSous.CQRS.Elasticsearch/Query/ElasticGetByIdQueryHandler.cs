namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query handler executing <see cref="ElasticGetByIdQuery{TDocument, TId}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticGetByIdQueryHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticGetByIdQueryHandler<TDocument, TId>>? logger = null)
    : IKyrolusQueryHandler<ElasticGetByIdQuery<TDocument, TId>, TDocument?>
    where TDocument : class
{
    public async Task<TDocument?> Handle(ElasticGetByIdQuery<TDocument, TId> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Retrieving '{DocumentType}' document by Id '{Id}'",
            typeof(TDocument).Name,
            query.Id);

        return await repository.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
    }
}
