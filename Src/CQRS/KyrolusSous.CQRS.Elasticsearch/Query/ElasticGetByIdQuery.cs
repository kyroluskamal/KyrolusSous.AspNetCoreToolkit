namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query retrieving a document by its identifier directly from an Elasticsearch index.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
/// <typeparam name="TId">The document identifier type.</typeparam>
public sealed record ElasticGetByIdQuery<TDocument, TId>(TId Id) : IKyrolusQuery<TDocument?>, IKyrolusCacheableRequest
    where TDocument : class
{
    /// <inheritdoc />
    public bool Cacheable { get; set; }

    /// <inheritdoc />
    public bool IsSharedAcrossUsers => true;
}
