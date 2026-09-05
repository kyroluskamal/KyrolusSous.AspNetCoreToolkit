namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query for executing smart full-text, fuzzy, and filtered searches over an Elasticsearch index.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
public sealed record ElasticSearchQuery<TDocument>(
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 10,
    bool EnableFuzzy = false)
    : IKyrolusQuery<KyrolusSearchResult<TDocument>>, IKyrolusCacheableRequest, IKyrolusThrottledRequest
    where TDocument : class
{
    /// <summary>Specific fields to search against. If empty, searches all mapped fields or default query fields.</summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>Fuzziness level (e.g., "AUTO", "1", "2"). Default: "AUTO".</summary>
    public string Fuzziness { get; init; } = "AUTO";

    /// <summary>Optional sort field name.</summary>
    public string? SortField { get; init; }

    /// <summary>Whether sorting is descending. Default: false.</summary>
    public bool SortDescending { get; init; }

    /// <summary>Fields to include in search highlights.</summary>
    public IReadOnlyList<string>? HighlightFields { get; init; }

    /// <summary>Optional custom configuration delegate for fine-grained filters, ranges, aggregations, or boosters.</summary>
    public Action<KyrolusSmartSearchBuilder<TDocument>>? CustomConfigure { get; init; }

    /// <summary>
    /// Optional allow-list restricting which field names <see cref="Fields"/> and <see cref="SortField"/>
    /// may reference. <see langword="null"/> (the default) is unrestricted - existing callers who never
    /// set it keep querying/sorting on whatever field names they always could. Mirrors
    /// <c>IKyrolusPropertyUpdateRequest.AllowedProperties</c>'s write-side opt-in shape for this
    /// read-side, caller-supplied field-name surface.
    /// </summary>
    public IReadOnlySet<string>? AllowedFields { get; init; }

    /// <inheritdoc />
    public bool Cacheable { get; set; }

    /// <inheritdoc />
    public bool IsSharedAcrossUsers => true;

    /// <inheritdoc />
    public string ThrottleKey => $"elastic:search:{typeof(TDocument).Name}";

    /// <inheritdoc />
    public int MaxConcurrentExecutions => 50;

    /// <inheritdoc />
    public TimeSpan ThrottleTimeout => TimeSpan.FromSeconds(15);
}
