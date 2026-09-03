namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query executing vector similarity (dense embeddings / kNN) search in Elasticsearch.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
public sealed record ElasticVectorSearchQuery<TDocument>(
    float[] Vector,
    string VectorField = "embedding",
    int TopK = 10)
    : IKyrolusQuery<KyrolusSearchResult<TDocument>>, IKyrolusCacheableRequest, IKyrolusThrottledRequest
    where TDocument : class
{
    /// <summary>Number of approximate nearest neighbors candidates to evaluate per shard. Default: TopK * 2.</summary>
    public int? NumCandidates { get; init; }

    /// <inheritdoc />
    public bool Cacheable { get; set; }

    /// <inheritdoc />
    public bool IsSharedAcrossUsers => true;

    /// <inheritdoc />
    public string ThrottleKey => $"elastic:vector:{typeof(TDocument).Name}";

    /// <inheritdoc />
    public int MaxConcurrentExecutions => 25;

    /// <inheritdoc />
    public TimeSpan ThrottleTimeout => TimeSpan.FromSeconds(20);
}
