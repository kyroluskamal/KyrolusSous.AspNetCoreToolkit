namespace KyrolusSous.Elasticsearch;

public class ElasticIndexManager(
    ElasticsearchClient client,
    IOptions<KyrolusElasticsearchOptions> options,
    ILogger<ElasticIndexManager>? logger = null) : IElasticIndexManager
{
    private readonly ElasticsearchClient _client = client;
    private readonly KyrolusElasticsearchOptions _options = options.Value;
    private readonly ILogger<ElasticIndexManager>? _logger = logger;

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var formattedName = FormatIndexName(indexName);
        var response = await _client.Indices.ExistsAsync(formattedName, cancellationToken);
        return response.Exists;
    }

    public async Task<bool> CreateIndexAsync<TDocument>(CancellationToken cancellationToken = default) where TDocument : class
    {
        var attr = typeof(TDocument).GetCustomAttribute<ElasticIndexAttribute>();
        var indexName = attr?.IndexName ?? typeof(TDocument).Name.ToLowerInvariant();
        var shards = attr?.NumberOfShards ?? 1;
        var replicas = attr?.NumberOfReplicas ?? 1;

        var formattedName = FormatIndexName(indexName);
        var exists = await IndexExistsAsync(formattedName, cancellationToken);
        if (exists)
        {
            _logger?.LogInformation("Elasticsearch index '{IndexName}' already exists.", formattedName);
            return true;
        }

        var response = await _client.Indices.CreateAsync<TDocument>(formattedName, descriptor =>
        {
            descriptor.Settings(s => s
                .NumberOfShards(shards)
                .NumberOfReplicas(replicas));

            descriptor.Mappings(m =>
            {
                m.Properties(p =>
                {
                    ApplyDocumentPropertyMappings<TDocument>(p);
                });
            });
        }, cancellationToken);

        var success = response.IsValidResponse;

        if (success)
        {
            _logger?.LogInformation("Successfully created Elasticsearch index '{IndexName}' with auto-generated mappings.", formattedName);
            if (attr is { UseAlias: true } && !string.IsNullOrWhiteSpace(attr.Alias))
            {
                await PutAliasAsync(indexName, attr.Alias, cancellationToken);
            }
        }
        else
        {
            _logger?.LogError("Failed to create Elasticsearch index '{IndexName}': {Error}", formattedName, response.DebugInformation);
        }

        return success;
    }

    public async Task<bool> CreateIndexAsync(
        string indexName,
        int shards = 1,
        int replicas = 1,
        CancellationToken cancellationToken = default)
    {
        var formattedName = FormatIndexName(indexName);
        var exists = await IndexExistsAsync(formattedName, cancellationToken);
        if (exists)
        {
            _logger?.LogInformation("Elasticsearch index '{IndexName}' already exists.", formattedName);
            return true;
        }

        var response = await _client.Indices.CreateAsync(formattedName, descriptor => descriptor
            .Settings(s => s
                .NumberOfShards(shards)
                .NumberOfReplicas(replicas)),
            cancellationToken);

        if (response.IsValidResponse)
        {
            _logger?.LogInformation("Successfully created Elasticsearch index '{IndexName}'.", formattedName);
            return true;
        }

        _logger?.LogError("Failed to create Elasticsearch index '{IndexName}': {Error}", formattedName, response.DebugInformation);
        return false;
    }

    public async Task<bool> DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var formattedName = FormatIndexName(indexName);
        var response = await _client.Indices.DeleteAsync(formattedName, cancellationToken);
        return response.IsValidResponse;
    }

    public async Task<bool> PutAliasAsync(string indexName, string aliasName, CancellationToken cancellationToken = default)
    {
        var formattedIndex = FormatIndexName(indexName);
        var formattedAlias = FormatIndexName(aliasName);

        var response = await _client.Indices.PutAliasAsync(formattedIndex, formattedAlias, cancellationToken);
        return response.IsValidResponse;
    }

    public async Task<bool> RemoveAliasAsync(string indexName, string aliasName, CancellationToken cancellationToken = default)
    {
        var formattedIndex = FormatIndexName(indexName);
        var formattedAlias = FormatIndexName(aliasName);

        var response = await _client.Indices.DeleteAliasAsync(formattedIndex, formattedAlias, cancellationToken);
        return response.IsValidResponse;
    }

    public async Task<bool> SwapAliasAsync(string aliasName, string oldIndexName, string newIndexName, CancellationToken cancellationToken = default)
    {
        var formattedAlias = FormatIndexName(aliasName);
        var formattedOld = FormatIndexName(oldIndexName);
        var formattedNew = FormatIndexName(newIndexName);

        var removeResponse = await _client.Indices.DeleteAliasAsync(formattedOld, formattedAlias, cancellationToken);
        var putResponse = await _client.Indices.PutAliasAsync(formattedNew, formattedAlias, cancellationToken);

        return removeResponse.IsValidResponse && putResponse.IsValidResponse;
    }

    public async Task<bool> CreateMonthlyIndexAsync<TDocument>(DateTime date, CancellationToken cancellationToken = default) where TDocument : class
    {
        var attr = typeof(TDocument).GetCustomAttribute<ElasticIndexAttribute>();
        var baseName = attr?.IndexName ?? typeof(TDocument).Name.ToLowerInvariant();
        var monthlyIndexName = $"{baseName}-{date:yyyy-MM}";
        var shards = attr?.NumberOfShards ?? 1;
        var replicas = attr?.NumberOfReplicas ?? 1;

        var success = await CreateIndexAsync(monthlyIndexName, shards, replicas, cancellationToken);
        if (success)
        {
            await PutAliasAsync(monthlyIndexName, baseName, cancellationToken);
        }

        return success;
    }

    public async Task<int> CleanupIndicesOlderThanAsync(string prefix, TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var formattedPrefix = FormatIndexName(prefix);
        var getResponse = await _client.Indices.GetAsync($"{formattedPrefix}*", cancellationToken);

        if (!getResponse.IsValidResponse)
        {
            return 0;
        }

        var deletedCount = 0;
        var cutoffDate = DateTime.UtcNow.Subtract(maxAge);

        foreach (var indexName in getResponse.Indices.Keys)
        {
            var nameString = indexName.ToString();
            if (TryExtractDateFromIndex(nameString, out var indexDate) && indexDate < cutoffDate)
            {
                var deleteResponse = await _client.Indices.DeleteAsync(nameString, cancellationToken);
                if (deleteResponse.IsValidResponse)
                {
                    _logger?.LogInformation("Cleaned up expired Elasticsearch index: '{IndexName}'", nameString);
                    deletedCount++;
                }
            }
        }

        return deletedCount;
    }

    private static void ApplyDocumentPropertyMappings<TDocument>(PropertiesDescriptor<TDocument> descriptor) where TDocument : class
    {
        var properties = typeof(TDocument).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var propName = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
            var propertyName = new PropertyName(propName);

            var textAttr = prop.GetCustomAttribute<ElasticTextAttribute>();
            var keywordAttr = prop.GetCustomAttribute<ElasticKeywordAttribute>();
            var geoAttr = prop.GetCustomAttribute<ElasticGeoPointAttribute>();
            var vectorAttr = prop.GetCustomAttribute<ElasticDenseVectorAttribute>();

            if (textAttr is not null)
            {
                descriptor.Text(propertyName, t =>
                {
                    if (!string.IsNullOrWhiteSpace(textAttr.Analyzer))
                    {
                        t.Analyzer(textAttr.Analyzer);
                    }
                    if (!string.IsNullOrWhiteSpace(textAttr.SearchAnalyzer))
                    {
                        t.SearchAnalyzer(textAttr.SearchAnalyzer);
                    }
                    t.Index(textAttr.Index);
                });
            }
            else if (keywordAttr is not null)
            {
                descriptor.Keyword(propertyName, k =>
                {
                    k.Index(keywordAttr.Index);
                    if (keywordAttr.IgnoreAbove)
                    {
                        k.IgnoreAbove(keywordAttr.MaxLength);
                    }
                });
            }
            else if (geoAttr is not null)
            {
                descriptor.GeoPoint(propertyName, _ => { });
            }
            else if (vectorAttr is not null)
            {
                descriptor.DenseVector(propertyName, v =>
                {
                    v.Dims(vectorAttr.Dimensions);
                    if (Enum.TryParse<DenseVectorSimilarity>(vectorAttr.Similarity, true, out var similarity))
                    {
                        v.Similarity(similarity);
                    }
                    else
                    {
                        v.Similarity(DenseVectorSimilarity.Cosine);
                    }
                });
            }
        }
    }

    private static bool TryExtractDateFromIndex(string indexName, out DateTime date)
    {
        date = default;
        var parts = indexName.Split('-');
        if (parts.Length < 2)
        {
            return false;
        }

        var datePart = string.Join("-", parts[^2..]);
        if (DateTime.TryParse(datePart, out date))
        {
            return true;
        }

        return DateTime.TryParse(parts[^1], out date);
    }

    private string FormatIndexName(string rawName)
    {
        var prefix = _options.IndexPrefix ?? string.Empty;
        var suffix = _options.IndexSuffix ?? string.Empty;
        return $"{prefix}{rawName}{suffix}".ToLowerInvariant();
    }
}
