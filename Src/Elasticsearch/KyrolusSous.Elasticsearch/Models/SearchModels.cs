namespace KyrolusSous.Elasticsearch;

public class SearchResult<TDocument>
{
    public IReadOnlyList<SearchHit<TDocument>> Hits { get; set; } = [];

    public IReadOnlyList<TDocument> Documents => [.. Hits.Select(h => h.Document)];

    public long Total { get; set; }

    public long TookMs { get; set; }

    public double? MaxScore { get; set; }

    public IDictionary<string, IReadOnlyList<FacetBucket>> Facets { get; set; } = new Dictionary<string, IReadOnlyList<FacetBucket>>();
}

public class SearchHit<TDocument>(TDocument document, string id, double? score = null, IDictionary<string, IReadOnlyList<string>>? highlights = null)
{
    public TDocument Document { get; set; } = document;

    public string Id { get; set; } = id;

    public double? Score { get; set; } = score;

    public IDictionary<string, IReadOnlyList<string>> Highlights { get; set; } = highlights ?? new Dictionary<string, IReadOnlyList<string>>();
}

public class FacetBucket(string key, long docCount)
{
    public string Key { get; set; } = key;

    public long DocCount { get; set; } = docCount;
}
