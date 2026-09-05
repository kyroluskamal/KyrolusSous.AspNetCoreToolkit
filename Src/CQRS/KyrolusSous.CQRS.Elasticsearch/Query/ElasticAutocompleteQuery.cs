namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query providing typeahead and autocomplete suggestions from an Elasticsearch index.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
public sealed record ElasticAutocompleteQuery<TDocument>(
    string Prefix,
    string TargetField,
    int MaxSuggestions = 5,
    bool Fuzzy = true)
    : IKyrolusQuery<IReadOnlyList<string>>, IKyrolusCacheableRequest, IKyrolusThrottledRequest
    where TDocument : class
{
    /// <summary>
    /// Optional allow-list restricting which field name <see cref="TargetField"/> may reference.
    /// <see langword="null"/> (the default) is unrestricted - existing callers who never set it keep
    /// autocompleting on whatever field name they always could. <see cref="TargetField"/> is used both
    /// as an Elasticsearch field reference and, in the handler, as a reflection target
    /// (<c>typeof(TDocument).GetProperty(...)</c>), so this closes the same allow-list gap
    /// <see cref="ElasticSearchQuery{TDocument}.AllowedFields"/> closes for <c>Fields</c>/<c>SortField</c>.
    /// </summary>
    public IReadOnlySet<string>? AllowedFields { get; init; }

    /// <inheritdoc />
    public bool Cacheable { get; set; } = true;

    /// <inheritdoc />
    public bool IsSharedAcrossUsers => true;

    /// <inheritdoc />
    public string ThrottleKey => $"elastic:autocomplete:{typeof(TDocument).Name}";

    /// <inheritdoc />
    public int MaxConcurrentExecutions => 100;

    /// <inheritdoc />
    public TimeSpan ThrottleTimeout => TimeSpan.FromSeconds(5);
}
