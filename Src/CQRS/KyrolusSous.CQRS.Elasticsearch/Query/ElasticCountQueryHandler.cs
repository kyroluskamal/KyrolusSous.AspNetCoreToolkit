namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query handler executing <see cref="ElasticCountQuery{TDocument}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticCountQueryHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticCountQueryHandler<TDocument, TId>>? logger = null)
    : IKyrolusQueryHandler<ElasticCountQuery<TDocument>, long>
    where TDocument : class
{
    public async Task<long> Handle(ElasticCountQuery<TDocument> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        logger?.LogDebug("[Kyrolus CQRS Elasticsearch] Counting documents in '{DocumentType}'", typeof(TDocument).Name);
        return await repository.CountAsync(cancellationToken).ConfigureAwait(false);
    }
}
