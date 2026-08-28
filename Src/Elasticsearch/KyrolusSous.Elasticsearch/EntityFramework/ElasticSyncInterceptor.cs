using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// EF Core SaveChangesInterceptor that automatically synchronizes entity mutations to Elasticsearch indices.
/// </summary>
public class KyrolusElasticSyncInterceptor(
    ElasticsearchClient client,
    IOptions<KyrolusElasticsearchOptions> options,
    ILogger<KyrolusElasticSyncInterceptor>? logger = null) : SaveChangesInterceptor
{
    private readonly ElasticsearchClient _client = client;
    private readonly KyrolusElasticsearchOptions _options = options.Value;
    private readonly ILogger<KyrolusElasticSyncInterceptor>? _logger = logger;

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null || result == 0)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        try
        {
            var entries = eventData.Context.ChangeTracker.Entries()
                .Where(e => e.Entity.GetType().GetCustomAttribute<KyrolusSyncToElasticsearchAttribute>() is not null)
                .ToList();

            foreach (var entry in entries)
            {
                var attr = entry.Entity.GetType().GetCustomAttribute<KyrolusSyncToElasticsearchAttribute>();
                var indexAttr = entry.Entity.GetType().GetCustomAttribute<KyrolusElasticIndexAttribute>();

                if (attr is null) continue;

                var indexName = attr.IndexName ?? indexAttr?.IndexName ?? entry.Entity.GetType().Name.ToLowerInvariant();
                var prefix = _options.IndexPrefix ?? string.Empty;
                var suffix = _options.IndexSuffix ?? string.Empty;
                var formattedIndex = $"{prefix}{indexName}{suffix}".ToLowerInvariant();

                var idProp = entry.Entity.GetType().GetProperty(attr.IdProperty);
                var idVal = idProp?.GetValue(entry.Entity)?.ToString();

                if (string.IsNullOrWhiteSpace(idVal))
                {
                    continue;
                }

                if (entry.State == EntityState.Deleted)
                {
                    await _client.DeleteAsync(new DeleteRequest(formattedIndex, idVal), cancellationToken);
                }
                else
                {
                    await _client.IndexAsync(entry.Entity, d => d.Index(formattedIndex).Id(idVal), cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to auto-sync EF Core changes to Elasticsearch.");
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
