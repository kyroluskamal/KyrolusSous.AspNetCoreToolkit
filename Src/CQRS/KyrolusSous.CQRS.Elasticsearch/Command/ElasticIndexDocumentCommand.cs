namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command indexing or replacing a document in Elasticsearch.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
/// <typeparam name="TId">The document identifier type.</typeparam>
public sealed record ElasticIndexDocumentCommand<TDocument, TId>(
    TDocument Document,
    TId Id) : IKyrolusCommand<bool>
    where TDocument : class;
