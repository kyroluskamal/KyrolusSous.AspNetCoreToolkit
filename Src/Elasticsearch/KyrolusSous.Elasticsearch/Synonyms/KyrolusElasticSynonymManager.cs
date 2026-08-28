using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Result of text analysis/tokenization.
/// </summary>
public sealed record KyrolusAnalyzedToken(string Token, int StartOffset, int EndOffset, string Type, int Position);

/// <summary>
/// Manager for text analysis and synonym rules on Elasticsearch indices.
/// </summary>
public interface IKyrolusElasticSynonymManager
{
    /// <summary>
    /// Analyzes a text string using an index's analyzer or a built-in analyzer (useful for debugging search queries and tokenizers).
    /// </summary>
    Task<IReadOnlyList<KyrolusAnalyzedToken>> AnalyzeTextAsync(string text, string? indexName = null, string? analyzer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates synonym rules on a closed index or creates an updated index template with custom synonyms.
    /// </summary>
    Task<bool> UpdateIndexSynonymsAsync(string indexName, string filterName, IEnumerable<string> synonyms, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IKyrolusElasticSynonymManager"/>.
/// </summary>
public class KyrolusElasticSynonymManager(
    ElasticsearchClient client,
    IOptions<KyrolusElasticsearchOptions>? options = null,
    ILogger<KyrolusElasticSynonymManager>? logger = null) : IKyrolusElasticSynonymManager
{
    private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly KyrolusElasticsearchOptions _options = options?.Value ?? new KyrolusElasticsearchOptions();
    private readonly ILogger<KyrolusElasticSynonymManager>? _logger = logger;

    public async Task<IReadOnlyList<KyrolusAnalyzedToken>> AnalyzeTextAsync(
        string text,
        string? indexName = null,
        string? analyzer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var response = await _client.Indices.AnalyzeAsync(d =>
        {
            d.Text([text]);
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                d.Index(FormatIndexName(indexName));
            }
            if (!string.IsNullOrWhiteSpace(analyzer))
            {
                d.Analyzer(analyzer);
            }
        }, cancellationToken);

        if (!response.IsValidResponse || response.Tokens is null)
        {
            return [];
        }

        return response.Tokens.Select(t => new KyrolusAnalyzedToken(
            Token: t.Token,
            StartOffset: (int)t.StartOffset,
            EndOffset: (int)t.EndOffset,
            Type: t.Type,
            Position: (int)t.Position
        )).ToList();
    }

    public async Task<bool> UpdateIndexSynonymsAsync(
        string indexName,
        string filterName,
        IEnumerable<string> synonyms,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterName);
        ArgumentNullException.ThrowIfNull(synonyms);

        var formattedIndex = FormatIndexName(indexName);
        var synonymList = synonyms.ToList();

        try
        {
            // Close index to update analysis settings
            await _client.Indices.CloseAsync(formattedIndex, cancellationToken);

            var settings = new IndexSettings
            {
                Analysis = new IndexSettingsAnalysis
                {
                    TokenFilters = new TokenFilters(new Dictionary<string, ITokenFilter>
                    {
                        { filterName, new SynonymTokenFilter { Synonyms = synonymList } }
                    })
                }
            };

            var response = await _client.Indices.PutSettingsAsync(settings, Indices.Index(formattedIndex), cancellationToken);

            // Reopen index
            await _client.Indices.OpenAsync(formattedIndex, cancellationToken);

            if (response.IsValidResponse)
            {
                _logger?.LogInformation("Successfully updated synonyms for filter '{Filter}' on index '{Index}'.", filterName, formattedIndex);
                return true;
            }

            _logger?.LogError("Failed to update synonyms on index '{Index}': {Error}", formattedIndex, response.DebugInformation);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception while updating synonyms on index '{Index}'.", formattedIndex);
            // Ensure index is reopened
            try { await _client.Indices.OpenAsync(formattedIndex, cancellationToken); } catch { /* Ignore */ }
            return false;
        }
    }

    private string FormatIndexName(string rawName)
    {
        var prefix = _options.IndexPrefix ?? string.Empty;
        var suffix = _options.IndexSuffix ?? string.Empty;
        return $"{prefix}{rawName}{suffix}".ToLowerInvariant();
    }
}
