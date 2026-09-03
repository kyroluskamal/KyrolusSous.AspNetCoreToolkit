namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command executing high-performance batch deletion of documents from Elasticsearch.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
/// <typeparam name="TId">The document identifier type.</typeparam>
public sealed record ElasticBulkDeleteCommand<TDocument, TId>(
    IEnumerable<TId> Ids) : IKyrolusCommand<KyrolusBulkResult>
    where TDocument : class;