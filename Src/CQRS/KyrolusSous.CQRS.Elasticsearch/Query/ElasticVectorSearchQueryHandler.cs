namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query handler executing <see cref="ElasticVectorSearchQuery{TDocument}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticVectorSearchQueryHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticVectorSearchQueryHandler<TDocument, TId>>? logger = null)
    : IKyrolusQueryHandler<ElasticVectorSearchQuery<TDocument>, KyrolusSearchResult<TDocument>>
    where TDocument : class
{
    public async Task<KyrolusSearchResult<TDocument>> Handle(ElasticVectorSearchQuery<TDocument> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Vector);

        var topK = Math.Clamp(query.TopK, 1, 100);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Executing vector search on '{DocumentType}' field '{VectorField}' (Dimensions: {Dims}, TopK: {TopK})",
            typeof(TDocument).Name,
            query.VectorField,
            query.Vector.Length,
            topK);

        return await repository.VectorSearchAsync(
            query.Vector,
            query.VectorField,
            topK,
            cancellationToken).ConfigureAwait(false);
    }
}
