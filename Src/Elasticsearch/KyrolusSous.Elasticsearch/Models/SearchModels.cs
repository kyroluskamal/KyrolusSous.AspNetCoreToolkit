namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Represents the result of an Elasticsearch search query.
/// </summary>
public class KyrolusSearchResult<TDocument>
{
    public IReadOnlyList<KyrolusSearchHit<TDocument>> Hits { get; set; } = [];

    public IReadOnlyList<TDocument> Documents => [.. Hits.Select(h => h.Document)];

    public long Total { get; set; }

    public long TookMs { get; set; }

    public double? MaxScore { get; set; }

    public IDictionary<string, IReadOnlyList<KyrolusFacetBucket>> Facets { get; set; } = new Dictionary<string, IReadOnlyList<KyrolusFacetBucket>>();

    public string? PitId { get; set; }

    public IReadOnlyList<object>? SearchAfterSortValues { get; set; }
}

/// <summary>
/// Backward-compatibility alias for <see cref="KyrolusSearchResult{TDocument}"/>.
/// </summary>
public class SearchResult<TDocument> : KyrolusSearchResult<TDocument>
{
}

/// <summary>
/// Represents a single hit in search results.
/// </summary>
public class KyrolusSearchHit<TDocument>(TDocument document, string id, double? score = null, IDictionary<string, IReadOnlyList<string>>? highlights = null)
{
    public TDocument Document { get; set; } = document;

    public string Id { get; set; } = id;

    public double? Score { get; set; } = score;

    public IDictionary<string, IReadOnlyList<string>> Highlights { get; set; } = highlights ?? new Dictionary<string, IReadOnlyList<string>>();

    public IReadOnlyList<object>? SortValues { get; set; }
}

/// <summary>
/// Backward-compatibility alias for <see cref="KyrolusSearchHit{TDocument}"/>.
/// </summary>
public class SearchHit<TDocument>(TDocument document, string id, double? score = null, IDictionary<string, IReadOnlyList<string>>? highlights = null)
    : KyrolusSearchHit<TDocument>(document, id, score, highlights)
{
}

/// <summary>
/// Represents a facet bucket aggregation entry.
/// </summary>
public class KyrolusFacetBucket(string key, long docCount)
{
    public string Key { get; set; } = key;

    public long DocCount { get; set; } = docCount;
}

/// <summary>
/// Backward-compatibility alias for <see cref="KyrolusFacetBucket"/>.
/// </summary>
public class FacetBucket(string key, long docCount) : KyrolusFacetBucket(key, docCount)
{
}

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
