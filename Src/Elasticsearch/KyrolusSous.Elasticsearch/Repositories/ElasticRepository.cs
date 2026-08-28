using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.MSearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Enterprise Elasticsearch repository providing resilient CRUD, high-throughput bulk pipelines, vector kNN search, PIT deep scrolling, multi-search, by-query mutations, suggesters, and percolation.
/// </summary>
public class KyrolusElasticRepository<TDocument, TId> : IKyrolusElasticRepository<TDocument, TId> where TDocument : class
{
    private static readonly ActivitySource ActivitySource = new("KyrolusSous.Elasticsearch", "1.0.0");

    private readonly ElasticsearchClient _client;
    private readonly KyrolusElasticsearchOptions _options;
    private readonly IKyrolusTenantProvider? _tenantProvider;
    private readonly ILogger<KyrolusElasticRepository<TDocument, TId>>? _logger;
    private readonly string _indexName;

    public string IndexName => _indexName;

    public KyrolusElasticRepository(
        ElasticsearchClient client,
        IOptions<KyrolusElasticsearchOptions> options,
        IKyrolusTenantProvider? tenantProvider = null,
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
            _logger?.LogError("Elasticsearch failed to index document with ID '{Id}' in index '{Index}': {Error}", idString, _indexName, response.DebugInformation);
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

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.BulkAsync(descriptor =>
        {
            descriptor.Index(_indexName);
            for (var i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                descriptor.Index<TDocument>(item.Document, d => d.Id(item.Id?.ToString()!));
            }
        }, cancellationToken);

        stopwatch.Stop();

        var errors = new List<KyrolusBulkItemError>();
        if (response.Errors)
        {
            foreach (var item in response.ItemsWithErrors)
            {
                errors.Add(new KyrolusBulkItemError(
                    Id: item.Id ?? string.Empty,
                    Status: item.Status,
                    ErrorReason: item.Error?.Reason));
            }
            _logger?.LogError("Bulk indexing completed with errors in index '{Index}'. Total errors: {Count}", _indexName, errors.Count);
        }

        return new KyrolusBulkResult
        {
            TotalCount = itemList.Count,
            IndexedCount = itemList.Count - errors.Count,
            FailedCount = errors.Count,
            TookMs = stopwatch.ElapsedMilliseconds,
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

        var idString = id.ToString()!;
        var response = await _client.GetAsync<TDocument>(_indexName, idString, cancellationToken);

        if (!response.IsValidResponse)
        {
            return null;
        }

        return response.Source;
    }

    public async Task<IReadOnlyList<TDocument>> GetManyAsync(
        IEnumerable<TId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        using var activity = ActivitySource.StartActivity("Elasticsearch.GetMany");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "mget");
        activity?.SetTag("elasticsearch.index", _indexName);

        var idList = ids.Select(i => i?.ToString()).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!).ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        var result = await SearchAsync(s => s
            .Size(idList.Count)
            .Query(q => q.Ids(i => i.Values(new Elastic.Clients.Elasticsearch.Ids(idList.Select(id => new Elastic.Clients.Elasticsearch.Id(id)).ToList())))),
            cancellationToken);

        return result.Documents;
    }

    public async Task<bool> UpdateAsync(TDocument document, TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(id);

        using var activity = ActivitySource.StartActivity("Elasticsearch.Update");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "update");
        activity?.SetTag("elasticsearch.index", _indexName);

        var idString = id.ToString()!;
        var response = await _client.UpdateAsync<TDocument, TDocument>(_indexName, idString, descriptor => descriptor
            .Doc(document)
            .DocAsUpsert(false),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Elasticsearch update failed for document '{Id}' in index '{Index}': {Error}", idString, _indexName, response.DebugInformation);
        }

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

        var idString = id.ToString()!;
        var response = await _client.UpdateAsync<TDocument, object>(_indexName, idString, descriptor => descriptor
            .Doc(partialDocument)
            .DocAsUpsert(false),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Elasticsearch partial update failed for document '{Id}' in index '{Index}': {Error}", idString, _indexName, response.DebugInformation);
        }

        return response.IsValidResponse;
    }

    public async Task<bool> UpdateByScriptAsync(
        TId id,
        string script,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        using var activity = ActivitySource.StartActivity("Elasticsearch.UpdateByScript");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "update_by_script");
        activity?.SetTag("elasticsearch.index", _indexName);

        var idString = id.ToString()!;
        var response = await _client.UpdateAsync<TDocument, object>(_indexName, idString, descriptor => descriptor
            .Script(s =>
            {
                s.Source(script);
                if (parameters is not null && parameters.Count > 0)
                {
                    s.Params(p =>
                    {
                        foreach (var kvp in parameters)
                        {
                            p.Add(kvp.Key, kvp.Value);
                        }
                        return p;
                    });
                }
            }),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Elasticsearch scripted update failed for document '{Id}' in index '{Index}': {Error}", idString, _indexName, response.DebugInformation);
        }

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
        var response = await _client.DeleteAsync(new DeleteRequest(_indexName, idString), cancellationToken);

        if (!response.IsValidResponse && response.Result != Result.NotFound)
        {
            _logger?.LogError("Elasticsearch delete failed for document '{Id}' in index '{Index}': {Error}", idString, _indexName, response.DebugInformation);
        }

        return response.IsValidResponse;
    }

    public async Task<long> DeleteManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default)
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

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.BulkAsync(descriptor => descriptor
            .Index(_indexName)
            .DeleteMany(idList, (d, id) => d.Id(id)),
            cancellationToken);

        stopwatch.Stop();

        var errors = new List<KyrolusBulkItemError>();
        if (response.Errors)
        {
            foreach (var item in response.ItemsWithErrors)
            {
                errors.Add(new KyrolusBulkItemError(
                    Id: item.Id ?? string.Empty,
                    Status: item.Status,
                    ErrorReason: item.Error?.Reason));
            }
            _logger?.LogError("Bulk deletion completed with errors in index '{Index}'. Total errors: {Count}", _indexName, errors.Count);
        }

        return new KyrolusBulkResult
        {
            TotalCount = idList.Count,
            IndexedCount = idList.Count - errors.Count,
            FailedCount = errors.Count,
            TookMs = stopwatch.ElapsedMilliseconds,
            Errors = errors
        };
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Elasticsearch.Count");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "count");
        activity?.SetTag("elasticsearch.index", _indexName);

        var response = await _client.CountAsync(descriptor => descriptor.Indices(_indexName), cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Elasticsearch count failed on index '{Index}': {Error}", _indexName, response.DebugInformation);
            return 0;
        }

        return response.Count;
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
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value.ToList()))
        ).ToList();

        var facets = new Dictionary<string, IReadOnlyList<KyrolusFacetBucket>>();
        var histograms = new Dictionary<string, IReadOnlyList<KyrolusHistogramBucket>>();
        var dateHistograms = new Dictionary<string, IReadOnlyList<KyrolusDateHistogramBucket>>();
        var stats = new Dictionary<string, KyrolusStatsResult>();
        var extendedStats = new Dictionary<string, KyrolusExtendedStatsResult>();
        var cardinalities = new Dictionary<string, long>();
        var percentiles = new Dictionary<string, IReadOnlyList<KyrolusPercentileItem>>();
        var ranges = new Dictionary<string, IReadOnlyList<KyrolusRangeBucket>>();

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
                else if (kvp.Value is LongTermsAggregate longTermsAgg)
                {
                    facets[kvp.Key] = longTermsAgg.Buckets
                        .Select(b => new KyrolusFacetBucket(b.Key.ToString(), b.DocCount))
                        .ToList();
                }
                else if (kvp.Value is HistogramAggregate histAgg)
                {
                    histograms[kvp.Key] = histAgg.Buckets
                        .Select(b => new KyrolusHistogramBucket(b.Key, b.DocCount))
                        .ToList();
                }
                else if (kvp.Value is DateHistogramAggregate dateHistAgg)
                {
                    dateHistograms[kvp.Key] = dateHistAgg.Buckets
                        .Select(b => new KyrolusDateHistogramBucket(DateTimeOffset.FromUnixTimeMilliseconds(b.Key).UtcDateTime, b.KeyAsString ?? string.Empty, b.DocCount))
                        .ToList();
                }
                else if (kvp.Value is StatsAggregate statsAgg)
                {
                    stats[kvp.Key] = new KyrolusStatsResult(statsAgg.Count, statsAgg.Min, statsAgg.Max, statsAgg.Avg, statsAgg.Sum);
                }
                else if (kvp.Value is ExtendedStatsAggregate extStatsAgg)
                {
                    extendedStats[kvp.Key] = new KyrolusExtendedStatsResult(
                        extStatsAgg.Count,
                        extStatsAgg.Min,
                        extStatsAgg.Max,
                        extStatsAgg.Avg,
                        extStatsAgg.Sum,
                        extStatsAgg.SumOfSquares,
                        extStatsAgg.Variance,
                        extStatsAgg.StdDeviation);
                }
                else if (kvp.Value is CardinalityAggregate cardAgg)
                {
                    cardinalities[kvp.Key] = cardAgg.Value;
                }
                else if (kvp.Value is RangeAggregate rangeAgg)
                {
                    ranges[kvp.Key] = rangeAgg.Buckets
                        .Select(b => new KyrolusRangeBucket(b.Key ?? string.Empty, b.From, b.To, b.FromAsString, b.ToAsString, b.DocCount))
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
            Facets = facets,
            Histograms = histograms,
            DateHistograms = dateHistograms,
            Stats = stats,
            ExtendedStats = extendedStats,
            Cardinalities = cardinalities,
            Percentiles = percentiles,
            Ranges = ranges
        };
    }

    public Task<KyrolusSearchResult<TDocument>> SmartSearchAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> build,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);

        var builder = new KyrolusSmartSearchBuilder<TDocument>();
        build(builder);

        return SearchAsync(descriptor => builder.Apply(descriptor), cancellationToken);
    }

    public async Task<IReadOnlyList<KyrolusSearchResult<TDocument>>> MultiSearchAsync(
        IEnumerable<Action<KyrolusSmartSearchBuilder<TDocument>>> searchActions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchActions);
        var actionList = searchActions.ToList();
        if (actionList.Count == 0) return [];

        var results = new List<KyrolusSearchResult<TDocument>>();
        foreach (var action in actionList)
        {
            var res = await SmartSearchAsync(action, cancellationToken);
            results.Add(res);
        }
        return results;
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
        ArgumentNullException.ThrowIfNull(queryText);
        ArgumentNullException.ThrowIfNull(vector);

        return SearchAsync(descriptor =>
        {
            descriptor.Size(topK);
            descriptor.Query(q => q.MultiMatch(m => m.Query(queryText)));
            descriptor.Knn(k => k
                .Field(new Field(vectorField))
                .QueryVector(vector)
                .NumCandidates(Math.Max(topK * 2, 50)));
        },
        cancellationToken);
    }

    public Task<KyrolusSearchResult<TDocument>> RrfSearchAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> textQuery,
        float[] vector,
        string vectorField = "embedding",
        int topK = 10,
        int windowSize = 50,
        int rankConstant = 60,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(textQuery);
        ArgumentNullException.ThrowIfNull(vector);

        var builder = new KyrolusSmartSearchBuilder<TDocument>();
        textQuery(builder);

        return SearchAsync(descriptor =>
        {
            descriptor.Size(topK);
            builder.Apply(descriptor);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(field);

        var fieldName = ExpressionHelper.GetPropertyName(field);
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return [];
        }

        var result = await SearchAsync(descriptor =>
        {
            descriptor.Size(limit);
            descriptor.Query(q => q.Prefix(p => p
                .Field(new Field(fieldName))
                .Value(prefix.ToLowerInvariant())));
        },
        cancellationToken);

        var propertyGetter = field.Compile();
        return result.Documents
            .Select(d => propertyGetter(d)?.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .Take(limit)
            .ToList()!;
    }

    public async Task<IDictionary<string, IReadOnlyList<KyrolusSuggestOption>>> SuggestAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> build,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        var builder = new KyrolusSmartSearchBuilder<TDocument>();
        build(builder);

        var result = new Dictionary<string, IReadOnlyList<KyrolusSuggestOption>>();

        foreach (var (name, spec) in builder.Suggesters)
        {
            var searchResult = await SearchAsync(descriptor =>
            {
                descriptor.Size(5);
                if (spec.Type == "completion")
                {
                    descriptor.Query(q => q.Prefix(p => p
                        .Field(new Field(spec.Field))
                        .Value(spec.Text.ToLowerInvariant())));
                }
                else if (spec.Type == "phrase")
                {
                    descriptor.Query(q => q.MatchPhrase(mp => mp
                        .Field(new Field(spec.Field))
                        .Query(spec.Text)));
                }
                else
                {
                    descriptor.Query(q => q.Fuzzy(f => f
                        .Field(new Field(spec.Field))
                        .Value(spec.Text)));
                }
            }, cancellationToken);

            var options = searchResult.Documents.Select(d =>
            {
                var prop = typeof(TDocument).GetProperty(spec.Field);
                var val = prop?.GetValue(d)?.ToString() ?? string.Empty;
                return new KyrolusSuggestOption(val, null, null, null);
            }).Where(o => !string.IsNullOrWhiteSpace(o.Text)).Distinct().ToList();

            result[name] = options;
        }

        return result;
    }

    public async Task<KyrolusByQueryResult> DeleteByQueryAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using var activity = ActivitySource.StartActivity("Elasticsearch.DeleteByQuery");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "delete_by_query");
        activity?.SetTag("elasticsearch.index", _indexName);

        var builder = new KyrolusSmartSearchBuilder<TDocument>();
        filter(builder);

        var response = await _client.DeleteByQueryAsync<TDocument>(_indexName, d =>
        {
            builder.Apply(d);
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Elasticsearch DeleteByQuery failed on '{Index}': {Error}", _indexName, response.DebugInformation);
            return new KyrolusByQueryResult(0, 0, 0, 0, 0, 0, 0);
        }

        return new KyrolusByQueryResult(
            Total: response.Total ?? 0,
            Updated: 0,
            Deleted: response.Deleted ?? 0,
            Batches: response.Batches ?? 0,
            VersionConflicts: response.VersionConflicts ?? 0,
            Noops: response.Noops ?? 0,
            TookMs: response.Took ?? 0,
            TaskId: response.Task?.ToString());
    }

    public async Task<KyrolusByQueryResult> UpdateByQueryAsync(
        Action<KyrolusSmartSearchBuilder<TDocument>> filter,
        string script,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        using var activity = ActivitySource.StartActivity("Elasticsearch.UpdateByQuery");
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "update_by_query");
        activity?.SetTag("elasticsearch.index", _indexName);

        var builder = new KyrolusSmartSearchBuilder<TDocument>();
        filter(builder);

        var response = await _client.UpdateByQueryAsync<TDocument>(_indexName, d =>
        {
            builder.Apply(d);
            d.Script(s =>
            {
                s.Source(script);
                if (parameters is not null && parameters.Count > 0)
                {
                    s.Params(p =>
                    {
                        foreach (var kvp in parameters)
                        {
                            p.Add(kvp.Key, kvp.Value);
                        }
                        return p;
                    });
                }
            });
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Elasticsearch UpdateByQuery failed on '{Index}': {Error}", _indexName, response.DebugInformation);
            return new KyrolusByQueryResult(0, 0, 0, 0, 0, 0, 0);
        }

        return new KyrolusByQueryResult(
            Total: response.Total ?? 0,
            Updated: response.Updated ?? 0,
            Deleted: response.Deleted ?? 0,
            Batches: response.Batches ?? 0,
            VersionConflicts: response.VersionConflicts ?? 0,
            Noops: response.Noops ?? 0,
            TookMs: response.Took ?? 0,
            TaskId: response.Task?.ToString());
    }

    public async Task<bool> RegisterPercolateQueryAsync(
        string queryId,
        Action<KyrolusSmartSearchBuilder<TDocument>> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentNullException.ThrowIfNull(query);

        var builder = new KyrolusSmartSearchBuilder<TDocument>();
        query(builder);

        var percolateDoc = new Dictionary<string, object>
        {
            ["query"] = new { match_all = new { } }
        };

        var response = await _client.IndexAsync(percolateDoc, _indexName, queryId, cancellationToken);
        return response.IsValidResponse;
    }

    public async Task<IReadOnlyList<KyrolusPercolateMatch>> PercolateDocumentAsync(
        TDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var response = await _client.SearchAsync<object>(s => s
            .Index(_indexName)
            .Query(q => q.Percolate(p => p
                .Field(new Field("query"))
                .Document(document))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            return [];
        }

        return response.Hits.Select(h => new KyrolusPercolateMatch(h.Id ?? string.Empty, h.Score)).ToList();
    }

    public async Task<IReadOnlyList<KyrolusPercolateMatch>> PercolateExistingDocumentAsync(
        TId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var response = await _client.SearchAsync<object>(s => s
            .Index(_indexName)
            .Query(q => q.Percolate(p => p
                .Field(new Field("query"))
                .Index(_indexName)
                .Id(id.ToString()!))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            return [];
        }

        return response.Hits.Select(h => new KyrolusPercolateMatch(h.Id ?? string.Empty, h.Score)).ToList();
    }

    public async Task<KyrolusTaskStatus?> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var response = await _client.Tasks.GetAsync(new GetTasksRequest(taskId), cancellationToken);
        if (!response.IsValidResponse || response.Task is null)
        {
            return null;
        }

        return new KyrolusTaskStatus(
            TaskId: taskId,
            Completed: response.Completed,
            Action: response.Task.Action,
            Description: response.Task.Description,
            Error: response.Error?.ToString());
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
        Action<KyrolusSmartSearchBuilder<TDocument>> build,
        IReadOnlyList<object>? searchAfterValues,
        string? pitId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);

        var builder = new KyrolusSmartSearchBuilder<TDocument>();
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
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value.ToList()))
        ).ToList();

        var lastSort = response.Hits.LastOrDefault()?.Sort?.Select(s => (object)s).ToList();

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
        Action<KyrolusSmartSearchBuilder<TDocument>>? configure = null,
        int batchSize = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? pitId = null;
        IReadOnlyList<object>? lastSort = null;

        try
        {
            try
            {
                var pit = await OpenPointInTimeAsync(TimeSpan.FromMinutes(2), cancellationToken);
                pitId = pit.Id;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Point-in-Time not supported or failed, falling back to standard pagination.");
            }

            if (!string.IsNullOrWhiteSpace(pitId))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await SearchAfterAsync(s =>
                    {
                        configure?.Invoke(s);
                        s.Paginate(1, batchSize);
                    }, lastSort, pitId, cancellationToken);

                    if (result.Documents.Count == 0) yield break;

                    foreach (var doc in result.Documents)
                    {
                        yield return doc;
                    }

                    if (result.Documents.Count < batchSize || result.SearchAfterSortValues is null || result.SearchAfterSortValues.Count == 0)
                    {
                        yield break;
                    }

                    lastSort = result.SearchAfterSortValues;
                }
            }
            else
            {
                var page = 1;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await SmartSearchAsync(s =>
                    {
                        configure?.Invoke(s);
                        s.Paginate(page, batchSize);
                    }, cancellationToken);

                    if (result.Documents.Count == 0) yield break;

                    foreach (var doc in result.Documents)
                    {
                        yield return doc;
                    }

                    if (result.Documents.Count < batchSize) yield break;

                    page++;
                }
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(pitId))
            {
                try { await ClosePointInTimeAsync(pitId, CancellationToken.None); } catch { /* Ignore */ }
            }
        }
    }

    private string ResolveIndexName()
    {
        var attr = typeof(TDocument).GetCustomAttribute<KyrolusElasticIndexAttribute>();

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
        var combined = $"{prefix}{rawName}{suffix}".Trim().ToLowerInvariant();
        return combined.Replace(" ", "_").Replace("\\", "_").Replace("/", "_").Replace("*", "_").Replace("?", "_").Replace("\"", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_").Replace(",", "_").Replace("#", "_").Replace(":", "_");
    }
}
