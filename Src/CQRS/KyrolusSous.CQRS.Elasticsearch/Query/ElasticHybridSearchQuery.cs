namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query executing hybrid search combining lexical (BM25) full-text and vector (dense embeddings) retrieval.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
public sealed record ElasticHybridSearchQuery<TDocument>(
    string QueryText,
    float[] Vector,
    string VectorField = "embedding",
    int TopK = 10)
    : IKyrolusQuery<KyrolusSearchResult<TDocument>>, IKyrolusCacheableRequest, IKyrolusThrottledRequest
    where TDocument : class
{
    /// <inheritdoc />
    public bool Cacheable { get; set; }

    /// <inheritdoc />
    public bool IsSharedAcrossUsers => true;

    /// <inheritdoc />
    public string ThrottleKey => $"elastic:hybrid:{typeof(TDocument).Name}";

    /// <inheritdoc />
    public int MaxConcurrentExecutions => 25;

    /// <inheritdoc />
    public TimeSpan ThrottleTimeout => TimeSpan.FromSeconds(20);
}
