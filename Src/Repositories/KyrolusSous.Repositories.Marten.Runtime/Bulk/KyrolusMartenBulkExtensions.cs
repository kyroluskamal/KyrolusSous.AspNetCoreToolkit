namespace KyrolusSous.Repositories.Marten.Runtime.Bulk;

/// <summary>
/// Provides high-speed batch and bulk insert operations leveraging PostgreSQL COPY through Marten.
/// </summary>
public static class KyrolusMartenBulkExtensions
{
    /// <summary>
    /// Bulk inserts a collection of documents directly into PostgreSQL via high-speed COPY protocol.
    /// </summary>
    public static Task BulkInsertDocumentsAsync<TDocument>(
        this IDocumentStore store,
        IReadOnlyCollection<TDocument> documents,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return Task.CompletedTask;
        }

        var normalizedBatchSize = batchSize <= 0 ? 1000 : batchSize;
        return store.BulkInsertAsync(documents, batchSize: normalizedBatchSize, cancellation: cancellationToken);
    }
}
