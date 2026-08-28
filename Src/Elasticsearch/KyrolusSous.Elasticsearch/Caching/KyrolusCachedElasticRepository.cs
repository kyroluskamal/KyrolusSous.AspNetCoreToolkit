using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// High-performance caching decorator for <see cref="IKyrolusElasticRepository{TDocument, TId}"/> with automatic cache invalidation on write mutations.
/// </summary>
public class KyrolusCachedElasticRepository<TDocument, TId> : IKyrolusElasticRepository<TDocument, TId> where TDocument : class
{
    private readonly IKyrolusElasticRepository<TDocument, TId> _inner;
    private readonly IKyrolusCacheProvider? _cacheProvider;
    private readonly TimeSpan _defaultTtl;

    public string IndexName => _inner.IndexName;

    public KyrolusCachedElasticRepository(
        IKyrolusElasticRepository<TDocument, TId> inner,
        IKyrolusCacheProvider? cacheProvider = null,
        TimeSpan? defaultTtl = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cacheProvider = cacheProvider;
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
    }

    public async Task<bool> AddAsync(TDocument document, TId id, CancellationToken cancellationToken = default)
    {
        var result = await _inner.AddAsync(document, id, cancellationToken);
        if (result && _cacheProvider is not null)
        {
            await InvalidateDocCacheAsync(id, cancellationToken);
        }
        return result;
    }

    public async Task<int> AddManyAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default)
    {
        var result = await _inner.AddManyAsync(items, cancellationToken);
        if (_cacheProvider is not null)
        {
            foreach (var item in items)
            {
                await InvalidateDocCacheAsync(item.Id, cancellationToken);
            }
        }
        return result;
    }

    public async Task<KyrolusBulkResult> BulkIndexAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default)
    {
        var result = await _inner.BulkIndexAsync(items, cancellationToken);
        if (_cacheProvider is not null)
        {
            foreach (var item in items)
            {
                await InvalidateDocCacheAsync(item.Id, cancellationToken);
            }
        }
        return result;
    }

    public async Task<TDocument?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        if (_cacheProvider is null)
        {
            return await _inner.GetByIdAsync(id, cancellationToken);
        }

        var cacheKey = $"es:{IndexName}:doc:{id}";
        var cached = await _cacheProvider.GetAsync<TDocument>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var doc = await _inner.GetByIdAsync(id, cancellationToken);
        if (doc is not null)
        {
            await _cacheProvider.SetAsync(cacheKey, doc, _defaultTtl, cancellationToken);
        }

        return doc;
    }

    public async Task<IReadOnlyList<TDocument>> GetManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];

        if (_cacheProvider is null)
        {
            return await _inner.GetManyAsync(idList, cancellationToken);
        }

        var results = new List<TDocument>();
        var missingIds = new List<TId>();

        foreach (var id in idList)
        {
            var cacheKey = $"es:{IndexName}:doc:{id}";
            var cached = await _cacheProvider.GetAsync<TDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                results.Add(cached);
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (missingIds.Count > 0)
        {
            var fetched = await _inner.GetManyAsync(missingIds, cancellationToken);
            foreach (var doc in fetched)
            {
                results.Add(doc);
            }
        }

        return results;
    }

    public async Task<bool> UpdateAsync(TDocument document, TId id, CancellationToken cancellationToken = default)
    {
        var result = await _inner.UpdateAsync(document, id, cancellationToken);
        if (result && _cacheProvider is not null)
        {
            await InvalidateDocCacheAsync(id, cancellationToken);
        }
        return result;
    }

    public async Task<bool> UpdatePartialAsync(TId id, object partialDocument, CancellationToken cancellationToken = default)
    {
        var result = await _inner.UpdatePartialAsync(id, partialDocument, cancellationToken);
        if (result && _cacheProvider is not null)
        {
            await InvalidateDocCacheAsync(id, cancellationToken);
        }
        return result;
    }

    public async Task<bool> UpdateByScriptAsync(TId id, string script, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        var result = await _inner.UpdateByScriptAsync(id, script, parameters, cancellationToken);
        if (result && _cacheProvider is not null)
        {
            await InvalidateDocCacheAsync(id, cancellationToken);
        }
        return result;
    }

    public async Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        var result = await _inner.DeleteAsync(id, cancellationToken);
        if (result && _cacheProvider is not null)
        {
            await InvalidateDocCacheAsync(id, cancellationToken);
        }
        return result;
    }

    public async Task<long> DeleteManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default)
    {
        var result = await _inner.DeleteManyAsync(ids, cancellationToken);
        if (_cacheProvider is not null)
        {
            foreach (var id in ids)
            {
                await InvalidateDocCacheAsync(id, cancellationToken);
            }
        }
        return result;
    }

    public async Task<KyrolusBulkResult> BulkDeleteAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default)
    {
        var result = await _inner.BulkDeleteAsync(ids, cancellationToken);
        if (_cacheProvider is not null)
        {
            foreach (var id in ids)
            {
                await InvalidateDocCacheAsync(id, cancellationToken);
            }
        }
        return result;
    }

    public Task<long> CountAsync(CancellationToken cancellationToken = default) => _inner.CountAsync(cancellationToken);

    public Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default) => _inner.ExistsAsync(id, cancellationToken);

    public Task<KyrolusSearchResult<TDocument>> SearchAsync(Action<SearchRequestDescriptor<TDocument>> configureSearch, CancellationToken cancellationToken = default) =>
        _inner.SearchAsync(configureSearch, cancellationToken);

    public Task<KyrolusSearchResult<TDocument>> SmartSearchAsync(Action<KyrolusSmartSearchBuilder<TDocument>> build, CancellationToken cancellationToken = default) =>
        _inner.SmartSearchAsync(build, cancellationToken);

    public Task<KyrolusSearchResult<TDocument>> VectorSearchAsync(float[] vector, string vectorField = "embedding", int topK = 10, CancellationToken cancellationToken = default) =>
        _inner.VectorSearchAsync(vector, vectorField, topK, cancellationToken);

    public Task<KyrolusSearchResult<TDocument>> HybridSearchAsync(string queryText, float[] vector, string vectorField = "embedding", int topK = 10, CancellationToken cancellationToken = default) =>
        _inner.HybridSearchAsync(queryText, vector, vectorField, topK, cancellationToken);

    public Task<KyrolusSearchResult<TDocument>> RrfSearchAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> textQuery,
        float[] vector,
        string vectorField = "embedding",
        int topK = 10,
        int windowSize = 50,
        int rankConstant = 60,
        CancellationToken cancellationToken = default) =>
        _inner.RrfSearchAsync(textQuery, vector, vectorField, topK, windowSize, rankConstant, cancellationToken);

    public Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, Expression<Func<TDocument, object>> field, int limit = 5, CancellationToken cancellationToken = default) =>
        _inner.AutocompleteAsync(prefix, field, limit, cancellationToken);

    public Task<KyrolusPointInTime> OpenPointInTimeAsync(TimeSpan keepAlive, CancellationToken cancellationToken = default) =>
        _inner.OpenPointInTimeAsync(keepAlive, cancellationToken);

    public Task<bool> ClosePointInTimeAsync(string pitId, CancellationToken cancellationToken = default) =>
        _inner.ClosePointInTimeAsync(pitId, cancellationToken);

    public Task<KyrolusSearchResult<TDocument>> SearchAfterAsync(Action<KyrolusSmartSearchBuilder<TDocument>> build, IReadOnlyList<object>? searchAfterValues, string? pitId = null, CancellationToken cancellationToken = default) =>
        _inner.SearchAfterAsync(build, searchAfterValues, pitId, cancellationToken);

    public Task<IReadOnlyList<KyrolusSearchResult<TDocument>>> MultiSearchAsync(
        IEnumerable<Action<KyrolusSmartSearchBuilder<TDocument>>> searchActions,
        CancellationToken cancellationToken = default) =>
        _inner.MultiSearchAsync(searchActions, cancellationToken);

    public Task<IDictionary<string, IReadOnlyList<KyrolusSuggestOption>>> SuggestAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> build,
        CancellationToken cancellationToken = default) =>
        _inner.SuggestAsync(build, cancellationToken);

    public async Task<KyrolusByQueryResult> DeleteByQueryAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> filter,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.DeleteByQueryAsync(filter, cancellationToken);
        if (_cacheProvider is not null && result.Deleted > 0)
        {
            await _cacheProvider.RemoveKeysByPatternAsync($"es:{IndexName}:doc:*", cancellationToken);
        }
        return result;
    }

    public async Task<KyrolusByQueryResult> UpdateByQueryAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> filter,
        string script,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.UpdateByQueryAsync(filter, script, parameters, cancellationToken);
        if (_cacheProvider is not null && result.Updated > 0)
        {
            await _cacheProvider.RemoveKeysByPatternAsync($"es:{IndexName}:doc:*", cancellationToken);
        }
        return result;
    }

    public Task<bool> RegisterPercolateQueryAsync(
        string queryId,
        Action<KyrolusSmartSearchBuilder<TDocument>> query,
        CancellationToken cancellationToken = default) =>
        _inner.RegisterPercolateQueryAsync(queryId, query, cancellationToken);

    public Task<IReadOnlyList<KyrolusPercolateMatch>> PercolateDocumentAsync(
        TDocument document,
        CancellationToken cancellationToken = default) =>
        _inner.PercolateDocumentAsync(document, cancellationToken);

    public Task<IReadOnlyList<KyrolusPercolateMatch>> PercolateExistingDocumentAsync(
        TId id,
        CancellationToken cancellationToken = default) =>
        _inner.PercolateExistingDocumentAsync(id, cancellationToken);

    public Task<KyrolusTaskStatus?> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default) =>
        _inner.GetTaskStatusAsync(taskId, cancellationToken);

    public IAsyncEnumerable<TDocument> StreamAllAsync(Action<KyrolusSmartSearchBuilder<TDocument>>? configure = null, int batchSize = 1000, CancellationToken cancellationToken = default) =>
        _inner.StreamAllAsync(configure, batchSize, cancellationToken);

    private async Task InvalidateDocCacheAsync(TId id, CancellationToken cancellationToken)
    {
        if (_cacheProvider is not null)
        {
            var cacheKey = $"es:{IndexName}:doc:{id}";
            await _cacheProvider.RemoveAsync(cacheKey, cancellationToken);
        }
    }
}
