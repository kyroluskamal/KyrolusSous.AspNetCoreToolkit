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

        var success = await CreateIndexAsync(indexName, shards, replicas, cancellationToken);

        if (success && attr is { UseAlias: true } && !string.IsNullOrWhiteSpace(attr.Alias))
        {
            await PutAliasAsync(indexName, attr.Alias, cancellationToken);
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

    private string FormatIndexName(string rawName)
    {
        var prefix = _options.IndexPrefix ?? string.Empty;
        var suffix = _options.IndexSuffix ?? string.Empty;
        return $"{prefix}{rawName}{suffix}".ToLowerInvariant();
    }
}
