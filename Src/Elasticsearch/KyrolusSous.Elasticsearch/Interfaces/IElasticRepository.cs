using System.Linq.Expressions;
using Elastic.Clients.Elasticsearch;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Primary enterprise abstraction for Elasticsearch document operations, smart querying, vector search, PIT pagination, multi-search, by-query mutations, suggesters, and percolation.
/// </summary>
public interface IKyrolusElasticRepository<TDocument, TId> where TDocument : class
{
    string IndexName { get; }

    Task<bool> AddAsync(TDocument document, TId id, CancellationToken cancellationToken = default);

    Task<int> AddManyAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default);

    Task<KyrolusBulkResult> BulkIndexAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default);

    Task<TDocument?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TDocument>> GetManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(TDocument document, TId id, CancellationToken cancellationToken = default);

    Task<bool> UpdatePartialAsync(TId id, object partialDocument, CancellationToken cancellationToken = default);

    Task<bool> UpdateByScriptAsync(TId id, string script, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default);

    Task<long> DeleteManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default);

    Task<KyrolusBulkResult> BulkDeleteAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default);

    Task<long> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);

    Task<KyrolusSearchResult<TDocument>> SearchAsync(
        Action<SearchRequestDescriptor<TDocument>> configureSearch,
        CancellationToken cancellationToken = default);

    Task<KyrolusSearchResult<TDocument>> SmartSearchAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> build,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KyrolusSearchResult<TDocument>>> MultiSearchAsync(
        IEnumerable<Action<KyrolusSmartSearchBuilder<TDocument>>> searchActions,
        CancellationToken cancellationToken = default);

    Task<KyrolusSearchResult<TDocument>> VectorSearchAsync(
        float[] vector,
        string vectorField = "embedding",
        int topK = 10,
        CancellationToken cancellationToken = default);

    Task<KyrolusSearchResult<TDocument>> HybridSearchAsync(
        string queryText,
        float[] vector,
        string vectorField = "embedding",
        int topK = 10,
        CancellationToken cancellationToken = default);

    Task<KyrolusSearchResult<TDocument>> RrfSearchAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> textQuery,
        float[] vector,
        string vectorField = "embedding",
        int topK = 10,
        int windowSize = 50,
        int rankConstant = 60,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> AutocompleteAsync(
        string prefix,
        Expression<Func<TDocument, object>> field,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<IDictionary<string, IReadOnlyList<KyrolusSuggestOption>>> SuggestAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> build,
        CancellationToken cancellationToken = default);

    Task<KyrolusByQueryResult> DeleteByQueryAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> filter,
        CancellationToken cancellationToken = default);

    Task<KyrolusByQueryResult> UpdateByQueryAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> filter,
        string script,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<bool> RegisterPercolateQueryAsync(
        string queryId,
        Action<KyrolusSmartSearchBuilder<TDocument>> query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KyrolusPercolateMatch>> PercolateDocumentAsync(
        TDocument document,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KyrolusPercolateMatch>> PercolateExistingDocumentAsync(
        TId id,
        CancellationToken cancellationToken = default);

    Task<KyrolusTaskStatus?> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task<KyrolusPointInTime> OpenPointInTimeAsync(TimeSpan keepAlive, CancellationToken cancellationToken = default);

    Task<bool> ClosePointInTimeAsync(string pitId, CancellationToken cancellationToken = default);

    Task<KyrolusSearchResult<TDocument>> SearchAfterAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> build,
        IReadOnlyList<object>? searchAfterValues,
        string? pitId = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<TDocument> StreamAllAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>>? configure = null,
        int batchSize = 1000,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Primary enterprise abstraction for Elasticsearch index lifecycle management, templates, aliases, ILM, reindexing, and maintenance.
/// </summary>
public interface IKyrolusElasticIndexManager
{
    Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default);

    Task<bool> CreateIndexAsync<TDocument>(CancellationToken cancellationToken = default) where TDocument : class;

    Task<bool> CreateIndexAsync(string indexName, int shards = 1, int replicas = 1, CancellationToken cancellationToken = default);

    Task<bool> DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default);

    Task<bool> PutAliasAsync(string indexName, string aliasName, CancellationToken cancellationToken = default);

    Task<bool> RemoveAliasAsync(string indexName, string aliasName, CancellationToken cancellationToken = default);

    Task<bool> SwapAliasAsync(string aliasName, string oldIndexName, string newIndexName, CancellationToken cancellationToken = default);

    Task<bool> CreateMonthlyIndexAsync<TDocument>(DateTime date, CancellationToken cancellationToken = default) where TDocument : class;

    Task<int> CleanupIndicesOlderThanAsync(string prefix, TimeSpan maxAge, CancellationToken cancellationToken = default);

    Task<KyrolusReindexResult> ReindexAsync(string sourceIndex, string destinationIndex, CancellationToken cancellationToken = default);

    Task<bool> CreateIlmPolicyAsync(string policyName, TimeSpan? hotMaxAge = null, long? hotMaxSizeBytes = null, TimeSpan? deleteMinAge = null, CancellationToken cancellationToken = default);

    Task<bool> RolloverIndexAsync(string aliasName, CancellationToken cancellationToken = default);

    Task<bool> ShrinkIndexAsync(string sourceIndex, string targetIndex, int targetShards = 1, CancellationToken cancellationToken = default);

    Task<bool> CreateIndexTemplateAsync(string templateName, string indexPattern, int priority = 100, int shards = 1, int replicas = 1, string? ilmPolicyName = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteIndexTemplateAsync(string templateName, CancellationToken cancellationToken = default);

    Task<bool> IndexTemplateExistsAsync(string templateName, CancellationToken cancellationToken = default);

    Task<bool> CreatePercolatorIndexAsync<TDocument>(string indexName, CancellationToken cancellationToken = default) where TDocument : class;

    Task<bool> RefreshIndexAsync(string indexName, CancellationToken cancellationToken = default);

    Task<bool> FlushIndexAsync(string indexName, CancellationToken cancellationToken = default);
}
