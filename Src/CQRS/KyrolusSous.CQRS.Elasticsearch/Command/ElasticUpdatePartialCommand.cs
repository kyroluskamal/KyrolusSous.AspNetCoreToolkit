namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command performing in-place partial updates to specific fields of an Elasticsearch document.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
/// <typeparam name="TId">The document identifier type.</typeparam>
public sealed record ElasticUpdatePartialCommand<TDocument, TId>(
    TId Id,
    object PartialDocument) : IKyrolusCommand<bool>
    where TDocument : class;
