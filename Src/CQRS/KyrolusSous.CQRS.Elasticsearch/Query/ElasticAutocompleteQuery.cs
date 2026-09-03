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
