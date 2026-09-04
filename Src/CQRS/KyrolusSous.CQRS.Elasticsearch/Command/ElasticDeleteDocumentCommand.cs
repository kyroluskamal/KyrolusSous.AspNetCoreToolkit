namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command deleting a document from Elasticsearch by its identifier.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
/// <typeparam name="TId">The document identifier type.</typeparam>
public sealed record ElasticDeleteDocumentCommand<TDocument, TId>(TId Id) : IKyrolusCommand<bool>
    where TDocument : class
{
    /// <summary>Optional expected sequence number for an Elasticsearch optimistic-concurrency check (see <see cref="ExpectedPrimaryTerm"/>). Both must be supplied together, or neither - a lone value is rejected by Elasticsearch itself.</summary>
    public long? ExpectedSeqNo { get; init; }

    /// <summary>Optional expected primary term for an Elasticsearch optimistic-concurrency check (see <see cref="ExpectedSeqNo"/>). Both must be supplied together, or neither.</summary>
    public long? ExpectedPrimaryTerm { get; init; }
}
