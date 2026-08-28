namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Represents the result of an Elasticsearch search query with documents, hits, metrics, aggregations, and suggestions.
/// </summary>
public sealed class KyrolusSearchResult<TDocument>
{
    public IReadOnlyList<KyrolusSearchHit<TDocument>> Hits { get; set; } = [];

    public IReadOnlyList<TDocument> Documents => [.. Hits.Select(h => h.Document)];

    public long Total { get; set; }

    public long TookMs { get; set; }

    public double? MaxScore { get; set; }

    public IDictionary<string, IReadOnlyList<KyrolusFacetBucket>> Facets { get; set; } = new Dictionary<string, IReadOnlyList<KyrolusFacetBucket>>();

    public IDictionary<string, IReadOnlyList<KyrolusHistogramBucket>> Histograms { get; set; } = new Dictionary<string, IReadOnlyList<KyrolusHistogramBucket>>();

    public IDictionary<string, IReadOnlyList<KyrolusDateHistogramBucket>> DateHistograms { get; set; } = new Dictionary<string, IReadOnlyList<KyrolusDateHistogramBucket>>();

    public IDictionary<string, KyrolusStatsResult> Stats { get; set; } = new Dictionary<string, KyrolusStatsResult>();

    public IDictionary<string, KyrolusExtendedStatsResult> ExtendedStats { get; set; } = new Dictionary<string, KyrolusExtendedStatsResult>();

    public IDictionary<string, long> Cardinalities { get; set; } = new Dictionary<string, long>();

    public IDictionary<string, IReadOnlyList<KyrolusPercentileItem>> Percentiles { get; set; } = new Dictionary<string, IReadOnlyList<KyrolusPercentileItem>>();

    public IDictionary<string, IReadOnlyList<KyrolusRangeBucket>> Ranges { get; set; } = new Dictionary<string, IReadOnlyList<KyrolusRangeBucket>>();

    public IDictionary<string, IReadOnlyList<KyrolusSuggestOption>> Suggestions { get; set; } = new Dictionary<string, IReadOnlyList<KyrolusSuggestOption>>();

    public string? PitId { get; set; }

    public IReadOnlyList<object>? SearchAfterSortValues { get; set; }
}

/// <summary>
/// Represents a single hit in search results.
/// </summary>
public sealed class KyrolusSearchHit<TDocument>(TDocument document, string id, double? score = null, IDictionary<string, IReadOnlyList<string>>? highlights = null)
{
    public TDocument Document { get; set; } = document;

    public string Id { get; set; } = id;

    public double? Score { get; set; } = score;

    public IDictionary<string, IReadOnlyList<string>> Highlights { get; set; } = highlights ?? new Dictionary<string, IReadOnlyList<string>>();

    public IReadOnlyList<object>? SortValues { get; set; }
}

/// <summary>
/// Represents a terms facet bucket aggregation entry.
/// </summary>
public sealed class KyrolusFacetBucket(string key, long docCount)
{
    public string Key { get; set; } = key;

    public long DocCount { get; set; } = docCount;
}

/// <summary>
/// Represents a numeric histogram bucket.
/// </summary>
public sealed record KyrolusHistogramBucket(double Key, long DocCount);

/// <summary>
/// Represents a date/time histogram bucket for time-series analytics.
/// </summary>
public sealed record KyrolusDateHistogramBucket(DateTime TimestampUtc, string KeyAsString, long DocCount);

/// <summary>
/// Represents statistical aggregate metrics (Count, Min, Max, Avg, Sum).
/// </summary>
public sealed record KyrolusStatsResult(long Count, double? Min, double? Max, double? Avg, double? Sum);

/// <summary>
/// Represents extended statistical metrics (SumOfSquares, Variance, StdDeviation, Bounds).
/// </summary>
public sealed record KyrolusExtendedStatsResult(
    long Count,
    double? Min,
    double? Max,
    double? Avg,
    double? Sum,
    double? SumOfSquares,
    double? Variance,
    double? StdDeviation);

/// <summary>
/// Represents a percentile item in percentile aggregations.
/// </summary>
public sealed record KyrolusPercentileItem(double Percentile, double? Value);

/// <summary>
/// Represents a range bucket entry (Numeric or Date range).
/// </summary>
public sealed record KyrolusRangeBucket(string Key, double? From, double? To, string? FromAsString, string? ToAsString, long DocCount);

/// <summary>
/// Represents a suggestion option returned by phrase, term, or completion suggesters.
/// </summary>
public sealed record KyrolusSuggestOption(string Text, double? Score, double? Freq, bool? Highlighted);

/// <summary>
/// Detailed result of a bulk indexing/deletion execution.
/// </summary>
public sealed class KyrolusBulkResult
{
    public bool HasErrors => FailedCount > 0;
    public int TotalCount { get; init; }
    public int IndexedCount { get; init; }
    public int FailedCount { get; init; }
    public long TookMs { get; init; }
    public IReadOnlyList<KyrolusBulkItemError> Errors { get; init; } = [];
}

/// <summary>
/// Information about an individual failed document in a bulk batch.
/// </summary>
public sealed record KyrolusBulkItemError(string Id, int Status, string? ErrorReason);

/// <summary>
/// Represents a Point-in-Time identifier for deep scroll-free pagination.
/// </summary>
public sealed record KyrolusPointInTime(string Id, TimeSpan KeepAlive);

/// <summary>
/// Result of an index reindexing operation.
/// </summary>
public sealed record KyrolusReindexResult(long Total, long Updated, long Created, long Deleted, long VersionConflicts, long TookMs);

/// <summary>
/// Result of an asynchronous DeleteByQuery or UpdateByQuery execution.
/// </summary>
public sealed record KyrolusByQueryResult(
    long Total,
    long Updated,
    long Deleted,
    long Batches,
    long VersionConflicts,
    long Noops,
    long TookMs,
    string? TaskId = null);

/// <summary>
/// Represents a background task status.
/// </summary>
public sealed record KyrolusTaskStatus(string TaskId, bool Completed, string? Action, string? Description, string? Error);

/// <summary>
/// Represents a matching query in a Percolator reverse search.
/// </summary>
public sealed record KyrolusPercolateMatch(string QueryId, double? Score, Dictionary<string, object>? Highlight = null);

/// <summary>
/// Represents a single search query specification for multi-search (msearch) batch execution.
/// </summary>
public sealed class KyrolusMultiSearchQuery<TDocument> where TDocument : class
{
    public string? IndexName { get; init; }

    public Action<KyrolusSmartSearchBuilder<TDocument>> BuilderAction { get; init; }

    public KyrolusMultiSearchQuery(Action<KyrolusSmartSearchBuilder<TDocument>> builderAction, string? indexName = null)
    {
        BuilderAction = builderAction ?? throw new ArgumentNullException(nameof(builderAction));
        IndexName = indexName;
    }
}
