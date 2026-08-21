namespace KyrolusSous.Elasticsearch;

public class ElasticRepository<TDocument, TId> : IElasticRepository<TDocument, TId> where TDocument : class
{
    private readonly ElasticsearchClient _client;
    private readonly KyrolusElasticsearchOptions _options;
    private readonly ILogger<ElasticRepository<TDocument, TId>>? _logger;
    private readonly string _indexName;

    public string IndexName => _indexName;

    public ElasticRepository(
        ElasticsearchClient client,
        IOptions<KyrolusElasticsearchOptions> options,
        ILogger<ElasticRepository<TDocument, TId>>? logger = null)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _indexName = ResolveIndexName();
    }

    public async Task<bool> AddAsync(TDocument document, TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(id);

        var idString = id.ToString()!;
        var response = await _client.IndexAsync(document, descriptor => descriptor
            .Index(_indexName)
            .Id(idString),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Failed to index document '{Id}' in '{Index}': {Error}", idString, _indexName, response.DebugInformation);
        }

        return response.IsValidResponse;
    }

    public async Task<int> AddManyAsync(
        IEnumerable<(TDocument Document, TId Id)> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return 0;
        }

        var batchSize = _options.BulkBatchSize > 0 ? _options.BulkBatchSize : 1000;
        var indexedCount = 0;

        for (var i = 0; i < itemList.Count; i += batchSize)
        {
            var batch = itemList.Skip(i).Take(batchSize).ToList();

            var response = await _client.BulkAsync(b => b
                .Index(_indexName)
                .IndexMany(batch.Select(x => x.Document), (descriptor, doc) =>
                {
                    var match = batch.First(bItem => ReferenceEquals(bItem.Document, doc));
                    descriptor.Id(match.Id?.ToString() ?? string.Empty);
                }),
                cancellationToken);

            if (response.IsValidResponse && !response.Errors)
            {
                indexedCount += batch.Count;
            }
            else
            {
                _logger?.LogError("Bulk indexing partially failed in '{Index}': {Error}", _indexName, response.DebugInformation);
                indexedCount += response.Items.Count(item => item.Status is >= 200 and < 300);
            }
        }

        return indexedCount;
    }

    public async Task<TDocument?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var response = await _client.GetAsync<TDocument>(
            id.ToString()!,
            descriptor => descriptor.Index(_indexName),
            cancellationToken);

        return response.IsValidResponse && response.Found ? response.Source : null;
    }

    public async Task<IReadOnlyList<TDocument>> GetManyAsync(
        IEnumerable<TId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idStrings = ids.Select(i => i?.ToString()).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!).ToList();
        if (idStrings.Count == 0)
        {
            return [];
        }

        var response = await _client.SearchAsync<TDocument>(s => s
            .Index(_indexName)
            .Query(q => q.Ids(i => i.Values(new Elastic.Clients.Elasticsearch.Ids(idStrings.Select(id => (Id)id).ToList())))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            return [];
        }

        return [.. response.Documents];
    }

    public async Task<bool> UpdateAsync(TDocument document, TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(id);

        var response = await _client.UpdateAsync<TDocument, TDocument>(
            _indexName,
            id.ToString()!,
            u => u.Doc(document).DocAsUpsert(true),
            cancellationToken);

        return response.IsValidResponse;
    }

    public async Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var idString = id.ToString()!;
        var response = await _client.DeleteAsync(
            new Elastic.Clients.Elasticsearch.DeleteRequest(_indexName, idString),
            cancellationToken);

        return response.IsValidResponse;
    }

    public async Task<long> DeleteManyAsync(
        IEnumerable<TId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.Select(i => i?.ToString()).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!).ToList();
        if (idList.Count == 0)
        {
            return 0;
        }

        var response = await _client.BulkAsync(b => b
            .Index(_indexName)
            .DeleteMany(idList),
            cancellationToken);

        return response.IsValidResponse ? response.Items.Count(item => item.Status is >= 200 and < 300) : 0;
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.CountAsync(c => c.Indices(_indexName), cancellationToken);
        return response.IsValidResponse ? response.Count : 0;
    }

    public async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var idString = id.ToString()!;
        var response = await _client.ExistsAsync(
            new Elastic.Clients.Elasticsearch.ExistsRequest(_indexName, idString),
            cancellationToken);

        return response.Exists;
    }

    public async Task<SearchResult<TDocument>> SearchAsync(
        Action<SearchRequestDescriptor<TDocument>> configureSearch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configureSearch);

        var stopwatch = Stopwatch.StartNew();

        var response = await _client.SearchAsync<TDocument>(descriptor =>
        {
            descriptor.Index(_indexName);
            configureSearch(descriptor);
        },
        cancellationToken);

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > _options.SlowQueryThresholdMs)
        {
            _logger?.LogWarning("Elasticsearch slow query on '{Index}': took {ElapsedMs} ms.", _indexName, stopwatch.ElapsedMilliseconds);
        }

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Elasticsearch search failed on '{Index}': {Error}", _indexName, response.DebugInformation);
            return new SearchResult<TDocument>
            {
                TookMs = stopwatch.ElapsedMilliseconds
            };
        }

        var hits = response.Hits.Select(hit => new SearchHit<TDocument>(
            hit.Source!,
            hit.Id ?? string.Empty,
            hit.Score,
            hit.Highlight?.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => (IReadOnlyList<string>)[.. kvp.Value]
            )
        )).ToList();

        return new SearchResult<TDocument>
        {
            Hits = hits,
            Total = response.Total,
            TookMs = response.Took,
            MaxScore = response.MaxScore
        };
    }

    private string ResolveIndexName()
    {
        var attr = typeof(TDocument).GetCustomAttribute<ElasticIndexAttribute>();
        if (attr is { UseAlias: true } && !string.IsNullOrWhiteSpace(attr.Alias))
        {
            return FormatIndexName(attr.Alias);
        }

        var baseName = attr?.IndexName ?? typeof(TDocument).Name.ToLowerInvariant();
        return FormatIndexName(baseName);
    }

    private string FormatIndexName(string rawName)
    {
        var prefix = _options.IndexPrefix ?? string.Empty;
        var suffix = _options.IndexSuffix ?? string.Empty;
        return $"{prefix}{rawName}{suffix}".ToLowerInvariant();
    }
}
