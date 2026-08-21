namespace KyrolusSous.Elasticsearch;

public interface IElasticRepository<TDocument, TId> where TDocument : class
{
    string IndexName { get; }

    Task<bool> AddAsync(TDocument document, TId id, CancellationToken cancellationToken = default);

    Task<int> AddManyAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default);

    Task<TDocument?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TDocument>> GetManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(TDocument document, TId id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default);

    Task<long> DeleteManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default);

    Task<long> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);

    Task<SearchResult<TDocument>> SearchAsync(
        Action<SearchRequestDescriptor<TDocument>> configureSearch,
        CancellationToken cancellationToken = default);
}

public interface IElasticIndexManager
{
    Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default);

    Task<bool> CreateIndexAsync<TDocument>(CancellationToken cancellationToken = default) where TDocument : class;

    Task<bool> CreateIndexAsync(string indexName, int shards = 1, int replicas = 1, CancellationToken cancellationToken = default);

    Task<bool> DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default);

    Task<bool> PutAliasAsync(string indexName, string aliasName, CancellationToken cancellationToken = default);

    Task<bool> RemoveAliasAsync(string indexName, string aliasName, CancellationToken cancellationToken = default);

    Task<bool> SwapAliasAsync(string aliasName, string oldIndexName, string newIndexName, CancellationToken cancellationToken = default);
}
