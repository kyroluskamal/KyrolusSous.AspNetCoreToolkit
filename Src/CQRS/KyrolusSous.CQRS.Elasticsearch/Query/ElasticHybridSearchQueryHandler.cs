namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query handler executing <see cref="ElasticHybridSearchQuery{TDocument}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticHybridSearchQueryHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticHybridSearchQueryHandler<TDocument, TId>>? logger = null)
    : IKyrolusQueryHandler<ElasticHybridSearchQuery<TDocument>, KyrolusSearchResult<TDocument>>
    where TDocument : class
{
    public async Task<KyrolusSearchResult<TDocument>> Handle(ElasticHybridSearchQuery<TDocument> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Vector);

        var topK = Math.Clamp(query.TopK, 1, 100);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Executing hybrid search on '{DocumentType}' (Text: '{Text}', TopK: {TopK})",
            typeof(TDocument).Name,
            query.QueryText,
            topK);

        return await repository.HybridSearchAsync(
            query.QueryText,
            query.Vector,
            query.VectorField,
            topK,
            cancellationToken).ConfigureAwait(false);
    }
}
