using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// EF Core SaveChangesInterceptor that safely captures entity state changes before persistence and synchronizes them to Elasticsearch upon successful commit.
/// </summary>
public class KyrolusElasticSyncInterceptor(
    ElasticsearchClient client,
    IOptions<KyrolusElasticsearchOptions> options,
    ILogger<KyrolusElasticSyncInterceptor>? logger = null) : SaveChangesInterceptor
{
    private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly KyrolusElasticsearchOptions _options = options?.Value ?? new KyrolusElasticsearchOptions();
    private readonly ILogger<KyrolusElasticSyncInterceptor>? _logger = logger;

    private readonly List<(string IndexName, string Id, object Entity, bool IsDeleted)> _pendingChanges = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _pendingChanges.Clear();

        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        try
        {
            var entries = eventData.Context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
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

                _pendingChanges.Add((formattedIndex, idVal, entry.Entity, entry.State == EntityState.Deleted));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to capture EF Core entity changes for Elasticsearch synchronization.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null || _pendingChanges.Count == 0)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        var changesToSync = new List<(string IndexName, string Id, object Entity, bool IsDeleted)>(_pendingChanges);
        _pendingChanges.Clear();

        try
        {
            foreach (var (indexName, idVal, entity, isDeleted) in changesToSync)
            {
                if (isDeleted)
                {
                    await _client.DeleteAsync(new DeleteRequest(indexName, idVal), cancellationToken);
                }
                else
                {
                    await _client.IndexAsync(entity, d => d.Index(indexName).Id(idVal), cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to auto-sync EF Core changes to Elasticsearch after successful save.");
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
