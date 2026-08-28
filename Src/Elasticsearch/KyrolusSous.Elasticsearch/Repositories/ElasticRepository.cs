namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Enterprise Elasticsearch repository providing resilient CRUD, high-throughput bulk pipelines, vector kNN search, and PIT deep scrolling.
/// </summary>
public class KyrolusElasticRepository<TDocument, TId> : IKyrolusElasticRepository<TDocument, TId>, IElasticRepository<TDocument, TId> where TDocument : class
{
    private static readonly ActivitySource ActivitySource = new("KyrolusSous.Elasticsearch", "1.0.0");

    private readonly ElasticsearchClient _client;
    private readonly KyrolusElasticsearchOptions _options;
    private readonly ITenantProvider? _tenantProvider;
    private readonly ILogger<KyrolusElasticRepository<TDocument, TId>>? _logger;
    private readonly string _indexName;

    public string IndexName => _indexName;

    public KyrolusElasticRepository(
        ElasticsearchClient client,
        IOptions<KyrolusElasticsearchOptions> options,
        ITenantProvider? tenantProvider = null,
        ILogger<KyrolusElasticRepository<TDocument, TId>>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? new KyrolusElasticsearchOptions();
        _tenantProvider = tenantProvider;
        _logger = logger;
        _indexName = ResolveIndexName();
    }

    public async Task<bool> AddAsync(TDocument document, TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(id);

        using var activity = ActivitySource.StartActivity("Elasticsearch.Add");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "index");
        activity?.SetTag("elasticsearch.index", _indexName);

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
        var result = await BulkIndexAsync(items, cancellationToken);
        return result.IndexedCount;
    }

    public async Task<KyrolusBulkResult> BulkIndexAsync(
        IEnumerable<(TDocument Document, TId Id)> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        using var activity = ActivitySource.StartActivity("Elasticsearch.BulkIndex");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "bulk_index");
        activity?.SetTag("elasticsearch.index", _indexName);

        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return new KyrolusBulkResult();
        }

        var batchSize = _options.BulkBatchSize > 0 ? _options.BulkBatchSize : 1000;
        var indexedCount = 0;
        var failedCount = 0;
        var errors = new List<KyrolusBulkItemError>();
        var sw = Stopwatch.StartNew();

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
                foreach (var item in response.Items)
                {
                    if (item.Status is >= 200 and < 300)
                    {
                        indexedCount++;
                    }
                    else
                    {
                        failedCount++;
                        errors.Add(new KyrolusBulkItemError(item.Id ?? string.Empty, item.Status, item.Error?.Reason));
                    }
                }
            }
        }

        sw.Stop();
        return new KyrolusBulkResult
        {
            TotalCount = itemList.Count,
            IndexedCount = indexedCount,
            FailedCount = failedCount,
            TookMs = sw.ElapsedMilliseconds,
            Errors = errors
        };
    }

    public async Task<TDocument?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = ActivitySource.StartActivity("Elasticsearch.GetById");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "get");
        activity?.SetTag("elasticsearch.index", _indexName);

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

        using var activity = ActivitySource.StartActivity("Elasticsearch.GetMany");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "multi_get");
        activity?.SetTag("elasticsearch.index", _indexName);

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

        using var activity = ActivitySource.StartActivity("Elasticsearch.Update");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "update");
        activity?.SetTag("elasticsearch.index", _indexName);

        var response = await _client.UpdateAsync<TDocument, TDocument>(
            _indexName,
            id.ToString()!,
            u => u.Doc(document).DocAsUpsert(true),
            cancellationToken);

        return response.IsValidResponse;
    }

    public async Task<bool> UpdatePartialAsync(TId id, object partialDocument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(partialDocument);

        using var activity = ActivitySource.StartActivity("Elasticsearch.UpdatePartial");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "update_partial");
        activity?.SetTag("elasticsearch.index", _indexName);

        var response = await _client.UpdateAsync<TDocument, object>(
            _indexName,
            id.ToString()!,
            u => u.Doc(partialDocument).DocAsUpsert(true),
            cancellationToken);

        return response.IsValidResponse;
    }

    public async Task<bool> UpdateByScriptAsync(TId id, string script, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        using var activity = ActivitySource.StartActivity("Elasticsearch.UpdateByScript");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "update_script");
        activity?.SetTag("elasticsearch.index", _indexName);

        var response = await _client.UpdateAsync<TDocument, object>(
            _indexName,
            id.ToString()!,
            u => u.Script(s =>
            {
                s.Source(script);
                if (parameters is not null && parameters.Count > 0)
                {
                    s.Params(p =>
                    {
                        foreach (var (k, v) in parameters)
                        {
                            p.Add(k, v);
                        }
                        return p;
                    });
                }
            }),
            cancellationToken);

        return response.IsValidResponse;
    }

    public async Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = ActivitySource.StartActivity("Elasticsearch.Delete");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "delete");
        activity?.SetTag("elasticsearch.index", _indexName);

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
        var result = await BulkDeleteAsync(ids, cancellationToken);
        return result.IndexedCount;
    }

    public async Task<KyrolusBulkResult> BulkDeleteAsync(
        IEnumerable<TId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        using var activity = ActivitySource.StartActivity("Elasticsearch.BulkDelete");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "bulk_delete");
        activity?.SetTag("elasticsearch.index", _indexName);

        var idList = ids.Select(i => i?.ToString()).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!).ToList();
        if (idList.Count == 0)
        {
            return new KyrolusBulkResult();
        }

        var sw = Stopwatch.StartNew();
        var response = await _client.BulkAsync(b => b
            .Index(_indexName)
            .DeleteMany(idList),
            cancellationToken);

        sw.Stop();
        var deletedCount = response.IsValidResponse ? response.Items.Count(item => item.Status is >= 200 and < 300) : 0;
        var failedCount = response.Items.Count(item => item.Status is < 200 or >= 300);

        return new KyrolusBulkResult
        {
            TotalCount = idList.Count,
            IndexedCount = deletedCount,
            FailedCount = failedCount,
            TookMs = sw.ElapsedMilliseconds
        };
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Elasticsearch.Count");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "count");
        activity?.SetTag("elasticsearch.index", _indexName);

        var response = await _client.CountAsync(c => c.Indices(_indexName), cancellationToken);
        return response.IsValidResponse ? response.Count : 0;
    }

    public async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = ActivitySource.StartActivity("Elasticsearch.Exists");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "exists");
        activity?.SetTag("elasticsearch.index", _indexName);

        var idString = id.ToString()!;
        var response = await _client.ExistsAsync(
            new Elastic.Clients.Elasticsearch.ExistsRequest(_indexName, idString),
            cancellationToken);

        return response.Exists;
    }

    public async Task<KyrolusSearchResult<TDocument>> SearchAsync(
        Action<SearchRequestDescriptor<TDocument>> configureSearch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configureSearch);

        using var activity = ActivitySource.StartActivity("Elasticsearch.Search");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "search");
        activity?.SetTag("elasticsearch.index", _indexName);

        var stopwatch = Stopwatch.StartNew();

        var response = await _client.SearchAsync<TDocument>(descriptor =>
        {
            descriptor.Index(_indexName);
            configureSearch(descriptor);
        },
        cancellationToken);

        stopwatch.Stop();

        activity?.SetTag("elasticsearch.took_ms", response.Took);
        activity?.SetTag("elasticsearch.hits", response.Total);

        if (stopwatch.ElapsedMilliseconds > _options.SlowQueryThresholdMs)
        {
            _logger?.LogWarning("Elasticsearch slow query on '{Index}': took {ElapsedMs} ms.", _indexName, stopwatch.ElapsedMilliseconds);
        }

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Elasticsearch search failed on '{Index}': {Error}", _indexName, response.DebugInformation);
            return new KyrolusSearchResult<TDocument>
            {
                TookMs = stopwatch.ElapsedMilliseconds
            };
        }

        var hits = response.Hits.Select(hit => new KyrolusSearchHit<TDocument>(
            hit.Source!,
            hit.Id ?? string.Empty,
            hit.Score,
            hit.Highlight?.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => (IReadOnlyList<string>)[.. kvp.Value]
            )
        )
        {
            SortValues = hit.Sort?.Select(s => (object)s.ToString()!).ToList()
        }).ToList();

        var facets = new Dictionary<string, IReadOnlyList<KyrolusFacetBucket>>();
        if (response.Aggregations is not null)
        {
            foreach (var kvp in response.Aggregations)
            {
                if (kvp.Value is StringTermsAggregate termsAgg)
                {
                    facets[kvp.Key] = termsAgg.Buckets
                        .Select(b => new KyrolusFacetBucket(b.Key.ToString() ?? string.Empty, b.DocCount))
                        .ToList();
                }
            }
        }

        return new KyrolusSearchResult<TDocument>
        {
            Hits = hits,
            Total = response.Total,
            TookMs = response.Took,
            MaxScore = response.MaxScore,
            Facets = facets
        };
    }

    public Task<KyrolusSearchResult<TDocument>> SmartSearchAsync(
        Action<SmartSearchBuilder<TDocument>> build,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);

        var builder = new SmartSearchBuilder<TDocument>();
        build(builder);

        return SearchAsync(descriptor => builder.Apply(descriptor), cancellationToken);
    }

    public Task<KyrolusSearchResult<TDocument>> VectorSearchAsync(
        float[] vector,
        string vectorField = "embedding",
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vector);

        return SearchAsync(descriptor =>
        {
            descriptor.Size(topK);
            descriptor.Knn(k => k
                .Field(new Field(vectorField))
                .QueryVector(vector)
                .NumCandidates(Math.Max(topK * 2, 50)));
        },
        cancellationToken);
    }

    public Task<KyrolusSearchResult<TDocument>> HybridSearchAsync(
        string queryText,
        float[] vector,
        string vectorField = "embedding",
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vector);

        return SearchAsync(descriptor =>
        {
            descriptor.Size(topK);
            if (!string.IsNullOrWhiteSpace(queryText))
            {
                descriptor.Query(q => q.QueryString(qs => qs.Query(queryText)));
            }

            descriptor.Knn(k => k
                .Field(new Field(vectorField))
                .QueryVector(vector)
                .NumCandidates(Math.Max(topK * 2, 50)));
        },
        cancellationToken);
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(
        string prefix,
        Expression<Func<TDocument, object>> field,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return [];
        }

        var propName = ExpressionHelper.GetPropertyName(field);
        if (string.IsNullOrWhiteSpace(propName))
        {
            return [];
        }

        var result = await SearchAsync(s => s
            .Size(limit)
            .Query(q => q.Prefix(p => p.Field(new Field(propName)).Value(prefix))),
            cancellationToken);

        var propertyGetter = field.Compile();
        return result.Documents
            .Select(d => propertyGetter(d)?.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList()!;
    }

    public async Task<KyrolusPointInTime> OpenPointInTimeAsync(TimeSpan keepAlive, CancellationToken cancellationToken = default)
    {
        var response = await _client.OpenPointInTimeAsync(_indexName, d => d.KeepAlive(new Elastic.Clients.Elasticsearch.Duration(keepAlive)), cancellationToken);

        if (!response.IsValidResponse || string.IsNullOrWhiteSpace(response.Id))
        {
            throw new InvalidOperationException($"Failed to open Point-in-Time for index '{_indexName}': {response.DebugInformation}");
        }

        return new KyrolusPointInTime(response.Id, keepAlive);
    }

    public async Task<bool> ClosePointInTimeAsync(string pitId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pitId);
        var response = await _client.ClosePointInTimeAsync(d => d.Id(pitId), cancellationToken);
        return response.IsValidResponse;
    }

    public async Task<KyrolusSearchResult<TDocument>> SearchAfterAsync(
        Action<SmartSearchBuilder<TDocument>> build,
        IReadOnlyList<object>? searchAfterValues,
        string? pitId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);

        var builder = new SmartSearchBuilder<TDocument>();
        build(builder);

        var response = await _client.SearchAsync<TDocument>(s =>
        {
            if (string.IsNullOrWhiteSpace(pitId))
            {
                s.Index(_indexName);
            }
            else
            {
                s.Pit(p => p.Id(pitId));
            }

            builder.Apply(s);

            if (searchAfterValues is not null && searchAfterValues.Count > 0)
            {
                s.SearchAfter(searchAfterValues.Select(v => (FieldValue)v.ToString()!).ToArray());
            }
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            return new KyrolusSearchResult<TDocument>();
        }

        var hits = response.Hits.Select(hit => new KyrolusSearchHit<TDocument>(
            hit.Source!,
            hit.Id ?? string.Empty,
            hit.Score,
            hit.Highlight?.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => (IReadOnlyList<string>)[.. kvp.Value]
            )
        )
        {
            SortValues = hit.Sort?.Select(s => (object)s.ToString()!).ToList()
        }).ToList();

        var lastSort = hits.LastOrDefault()?.SortValues;

        return new KyrolusSearchResult<TDocument>
        {
            Hits = hits,
            Total = response.Total,
            TookMs = response.Took,
            MaxScore = response.MaxScore,
            PitId = response.PitId ?? pitId,
            SearchAfterSortValues = lastSort
        };
    }

    public async IAsyncEnumerable<TDocument> StreamAllAsync(
        Action<SmartSearchBuilder<TDocument>>? configure = null,
        int batchSize = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await SmartSearchAsync(s =>
            {
                configure?.Invoke(s);
                s.Paginate(page, batchSize);
            }, cancellationToken);

            if (result.Documents.Count == 0)
            {
                yield break;
            }

            foreach (var doc in result.Documents)
            {
                yield return doc;
            }

            if (result.Documents.Count < batchSize)
            {
                yield break;
            }

            page++;
        }
    }

    private string ResolveIndexName()
    {
        var attr = typeof(TDocument).GetCustomAttribute<KyrolusElasticIndexAttribute>()
                   ?? typeof(TDocument).GetCustomAttribute<ElasticIndexAttribute>();

        var baseName = (attr is { UseAlias: true } && !string.IsNullOrWhiteSpace(attr.Alias))
            ? attr.Alias
            : attr?.IndexName ?? typeof(TDocument).Name.ToLowerInvariant();

        if (_options.EnableMultiTenancy &&
            _options.TenantIsolationMode == TenantIsolationMode.IndexPerTenant &&
            !string.IsNullOrWhiteSpace(_tenantProvider?.CurrentTenantId))
        {
            baseName = $"{_tenantProvider.CurrentTenantId}_{baseName}";
        }

        return FormatIndexName(baseName);
    }

    private string FormatIndexName(string rawName)
    {
        var prefix = _options.IndexPrefix ?? string.Empty;
        var suffix = _options.IndexSuffix ?? string.Empty;
        return $"{prefix}{rawName}{suffix}".ToLowerInvariant();
    }
}

/// <summary>
/// Backward-compatibility alias for <see cref="KyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public class ElasticRepository<TDocument, TId>(
    ElasticsearchClient client,
    IOptions<KyrolusElasticsearchOptions> options,
    ITenantProvider? tenantProvider = null,
    ILogger<ElasticRepository<TDocument, TId>>? logger = null)
    : KyrolusElasticRepository<TDocument, TId>(client, options, tenantProvider, logger) where TDocument : class
{
}
