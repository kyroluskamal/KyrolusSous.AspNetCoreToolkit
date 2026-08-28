using System.Diagnostics;
using System.Reflection;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Primary enterprise index manager for schema mapping generation, index templates, alias switching, reindexing, and ILM lifecycle policies.
/// </summary>
public class KyrolusElasticIndexManager(
    ElasticsearchClient client,
    IOptions<KyrolusElasticsearchOptions> options,
    ILogger<KyrolusElasticIndexManager>? logger = null) : IKyrolusElasticIndexManager
{
    private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly KyrolusElasticsearchOptions _options = options?.Value ?? new KyrolusElasticsearchOptions();
    private readonly ILogger<KyrolusElasticIndexManager>? _logger = logger;

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var formattedName = FormatIndexName(indexName);
        var response = await _client.Indices.ExistsAsync(formattedName, cancellationToken);
        return response.Exists;
    }

    public async Task<bool> CreateIndexAsync<TDocument>(CancellationToken cancellationToken = default) where TDocument : class
    {
        var attr = typeof(TDocument).GetCustomAttribute<KyrolusElasticIndexAttribute>();

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

        if (response.IsValidResponse)
        {
            _logger?.LogInformation("Successfully deleted Elasticsearch index '{IndexName}'.", formattedName);
            return true;
        }

        _logger?.LogError("Failed to delete Elasticsearch index '{IndexName}': {Error}", formattedName, response.DebugInformation);
        return false;
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
        var removed = await RemoveAliasAsync(oldIndexName, aliasName, cancellationToken);
        var added = await PutAliasAsync(newIndexName, aliasName, cancellationToken);

        if (added)
        {
            _logger?.LogInformation("Successfully swapped alias '{Alias}' from '{OldIndex}' to '{NewIndex}'.", aliasName, oldIndexName, newIndexName);
            return true;
        }

        _logger?.LogError("Failed to swap alias '{Alias}'.", aliasName);
        return false;
    }

    public async Task<bool> CreateMonthlyIndexAsync<TDocument>(DateTime date, CancellationToken cancellationToken = default) where TDocument : class
    {
        var attr = typeof(TDocument).GetCustomAttribute<KyrolusElasticIndexAttribute>();
        var baseName = attr?.IndexName ?? typeof(TDocument).Name.ToLowerInvariant();
        var monthlyIndexName = $"{baseName}-{date:yyyy-MM}";

        var created = await CreateIndexAsync<TDocument>(cancellationToken);
        if (created && attr is { UseAlias: true } && !string.IsNullOrWhiteSpace(attr.Alias))
        {
            await PutAliasAsync(monthlyIndexName, attr.Alias, cancellationToken);
        }

        return created;
    }

    public async Task<int> CleanupIndicesOlderThanAsync(string prefix, TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var formattedPrefix = FormatIndexName(prefix);
        var indicesResponse = await _client.Indices.GetAsync(Indices.Index($"{formattedPrefix}*"), cancellationToken);

        if (!indicesResponse.IsValidResponse || indicesResponse.Indices is null)
        {
            return 0;
        }

        var thresholdDate = DateTime.UtcNow.Subtract(maxAge);
        var deletedCount = 0;

        foreach (var (indexName, _) in indicesResponse.Indices)
        {
            var nameString = indexName.ToString();
            var parts = nameString.Split('-');
            if (parts.Length >= 2 && DateTime.TryParse(string.Join("-", parts.TakeLast(2)), out var indexDate))
            {
                if (indexDate < thresholdDate)
                {
                    var deleted = await DeleteIndexAsync(nameString, cancellationToken);
                    if (deleted)
                    {
                        deletedCount++;
                        _logger?.LogInformation("Deleted expired Elasticsearch index '{IndexName}'.", nameString);
                    }
                }
            }
        }

        return deletedCount;
    }

    public async Task<KyrolusReindexResult> ReindexAsync(string sourceIndex, string destinationIndex, CancellationToken cancellationToken = default)
    {
        var formattedSource = FormatIndexName(sourceIndex);
        var formattedDest = FormatIndexName(destinationIndex);

        var sw = Stopwatch.StartNew();
        var response = await _client.ReindexAsync(r => r
            .Source(s => s.Indices(Indices.Index(formattedSource)))
            .Dest(d => d.Index(formattedDest)),
            cancellationToken);

        sw.Stop();

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Reindex failed from '{Source}' to '{Dest}': {Error}", formattedSource, formattedDest, response.DebugInformation);
            return new KyrolusReindexResult(0, 0, 0, 0, 0, sw.ElapsedMilliseconds);
        }

        return new KyrolusReindexResult(
            Total: response.Total ?? 0,
            Updated: response.Updated ?? 0,
            Created: response.Created ?? 0,
            Deleted: response.Deleted ?? 0,
            VersionConflicts: response.VersionConflicts ?? 0,
            TookMs: sw.ElapsedMilliseconds
        );
    }

    public async Task<bool> CreateIlmPolicyAsync(
        string policyName,
        TimeSpan? hotMaxAge = null,
        long? hotMaxSizeBytes = null,
        TimeSpan? deleteMinAge = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.Indices.PutIndexTemplateAsync(policyName, cancellationToken);
            return response.IsValidResponse;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to configure ILM template '{PolicyName}'", policyName);
            return false;
        }
    }

    public async Task<bool> RolloverIndexAsync(string aliasName, CancellationToken cancellationToken = default)
    {
        var formattedAlias = FormatIndexName(aliasName);
        var response = await _client.Indices.RolloverAsync(formattedAlias, cancellationToken);
        return response.IsValidResponse && response.RolledOver;
    }

    public async Task<bool> ShrinkIndexAsync(string sourceIndex, string targetIndex, int targetShards = 1, CancellationToken cancellationToken = default)
    {
        var formattedSource = FormatIndexName(sourceIndex);
        var formattedTarget = FormatIndexName(targetIndex);

        var response = await _client.Indices.ShrinkAsync(formattedSource, formattedTarget, cancellationToken);
        return response.IsValidResponse;
    }

    public async Task<bool> CreateIndexTemplateAsync(
        string templateName,
        string indexPattern,
        int priority = 100,
        int shards = 1,
        int replicas = 1,
        string? ilmPolicyName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexPattern);

        var response = await _client.Indices.PutIndexTemplateAsync(templateName, descriptor =>
        {
            descriptor.IndexPatterns(indexPattern);
            descriptor.Priority(priority);
            descriptor.Template(t => t.Settings(s =>
            {
                s.NumberOfShards(shards);
                s.NumberOfReplicas(replicas);
            }));
        }, cancellationToken);

        return response.IsValidResponse;
    }

    public async Task<bool> DeleteIndexTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        var response = await _client.Indices.DeleteIndexTemplateAsync(templateName, cancellationToken);
        return response.IsValidResponse;
    }

    public async Task<bool> IndexTemplateExistsAsync(string templateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        var response = await _client.Indices.ExistsIndexTemplateAsync(templateName, cancellationToken);
        return response.Exists;
    }

    public async Task<bool> CreatePercolatorIndexAsync<TDocument>(string indexName, CancellationToken cancellationToken = default) where TDocument : class
    {
        var formattedName = FormatIndexName(indexName);
        var exists = await IndexExistsAsync(formattedName, cancellationToken);
        if (exists) return true;

        var response = await _client.Indices.CreateAsync<TDocument>(formattedName, d =>
        {
            d.Mappings(m =>
            {
                m.Properties(p =>
                {
                    p.Percolator("query");
                    ApplyDocumentPropertyMappings<TDocument>(p);
                });
            });
        }, cancellationToken);

        return response.IsValidResponse;
    }

    public async Task<bool> RefreshIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var formattedName = FormatIndexName(indexName);
        var response = await _client.Indices.RefreshAsync(formattedName, cancellationToken);
        return response.IsValidResponse;
    }

    public async Task<bool> FlushIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var formattedName = FormatIndexName(indexName);
        var response = await _client.Indices.FlushAsync(formattedName, cancellationToken);
        return response.IsValidResponse;
    }

    private static void ApplyDocumentPropertyMappings<TDocument>(PropertiesDescriptor<TDocument> descriptor)
    {
        var properties = typeof(TDocument).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var propName = prop.Name;
            var textAttr = prop.GetCustomAttribute<KyrolusElasticTextAttribute>();
            var keywordAttr = prop.GetCustomAttribute<KyrolusElasticKeywordAttribute>();
            var geoAttr = prop.GetCustomAttribute<KyrolusElasticGeoPointAttribute>();
            var vectorAttr = prop.GetCustomAttribute<KyrolusElasticDenseVectorAttribute>();
            var completionAttr = prop.GetCustomAttribute<KyrolusElasticCompletionAttribute>();
            var percolatorAttr = prop.GetCustomAttribute<KyrolusElasticPercolatorAttribute>();

            if (completionAttr is not null)
            {
                descriptor.Completion(propName, c =>
                {
                    c.Analyzer(completionAttr.Analyzer);
                    c.PreserveSeparators(completionAttr.PreserveSeparators);
                    c.PreservePositionIncrements(completionAttr.PreservePositionIncrements);
                    c.MaxInputLength(completionAttr.MaxInputLength);
                });
            }
            else if (percolatorAttr is not null)
            {
                descriptor.Percolator(propName);
            }
            else if (textAttr is not null)
            {
                descriptor.Text(propName, t =>
                {
                    t.Analyzer(textAttr.Analyzer);
                    if (!string.IsNullOrWhiteSpace(textAttr.SearchAnalyzer))
                    {
                        t.SearchAnalyzer(textAttr.SearchAnalyzer);
                    }
                    t.Index(textAttr.Index);
                });
            }
            else if (keywordAttr is not null)
            {
                descriptor.Keyword(propName, k =>
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
                descriptor.GeoPoint(propName);
            }
            else if (vectorAttr is not null)
            {
                descriptor.DenseVector(propName, v =>
                {
                    v.Dims(vectorAttr.Dimensions);
                    if (Enum.TryParse<DenseVectorSimilarity>(vectorAttr.Similarity, true, out var sim))
                    {
                        v.Similarity(sim);
                    }
                });
            }
        }
    }

    private string FormatIndexName(string rawName)
    {
        var prefix = _options.IndexPrefix ?? string.Empty;
        var suffix = _options.IndexSuffix ?? string.Empty;
        var combined = $"{prefix}{rawName}{suffix}".Trim().ToLowerInvariant();
        return combined.Replace(" ", "_").Replace("\\", "_").Replace("/", "_").Replace("*", "_").Replace("?", "_").Replace("\"", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_").Replace(",", "_").Replace("#", "_").Replace(":", "_");
    }
}
