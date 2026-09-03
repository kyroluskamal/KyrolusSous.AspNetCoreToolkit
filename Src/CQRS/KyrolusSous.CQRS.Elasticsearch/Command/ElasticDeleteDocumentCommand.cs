namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command deleting a document from Elasticsearch by its identifier.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
/// <typeparam name="TId">The document identifier type.</typeparam>
public sealed record ElasticDeleteDocumentCommand<TDocument, TId>(TId Id) : IKyrolusCommand<bool>
    where TDocument : class;
