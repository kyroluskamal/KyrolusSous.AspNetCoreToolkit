namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command executing high-performance batch indexing of documents in Elasticsearch.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
/// <typeparam name="TId">The document identifier type.</typeparam>
public sealed record ElasticBulkIndexCommand<TDocument, TId>(
    IEnumerable<(TDocument Document, TId Id)> Items) : IKyrolusCommand<KyrolusBulkResult>
    where TDocument : class;
