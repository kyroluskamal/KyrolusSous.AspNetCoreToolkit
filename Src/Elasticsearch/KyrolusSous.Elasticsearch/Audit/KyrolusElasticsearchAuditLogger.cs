namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Structured audit event payload for Elasticsearch.
/// </summary>
public record KyrolusElasticAuditEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Action { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? TenantId { get; init; }
    public string? Resource { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
    public string? Details { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Enterprise audit logger interface for indexing audit events into Elasticsearch with date-partitioned indices.
/// </summary>
public interface IKyrolusElasticsearchAuditLogger
{
    Task LogAsync(KyrolusElasticAuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task LogManyAsync(IEnumerable<KyrolusElasticAuditEvent> auditEvents, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IKyrolusElasticsearchAuditLogger"/>.
/// </summary>
public class KyrolusElasticsearchAuditLogger : IKyrolusElasticsearchAuditLogger
{
    private readonly ElasticsearchClient _client;
    private readonly KyrolusElasticsearchOptions _options;
    private readonly ILogger<KyrolusElasticsearchAuditLogger>? _logger;

    public KyrolusElasticsearchAuditLogger(
        ElasticsearchClient client,
        IOptions<KyrolusElasticsearchOptions> options,
        ILogger<KyrolusElasticsearchAuditLogger>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? new KyrolusElasticsearchOptions();
        _logger = logger;
    }

    public async Task LogAsync(KyrolusElasticAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var indexName = GetIndexName(auditEvent.Timestamp);
        var response = await _client.IndexAsync(auditEvent, d => d.Index(indexName).Id(auditEvent.Id), cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger?.LogError("Failed to index audit event '{Id}' to '{Index}': {Error}", auditEvent.Id, indexName, response.DebugInformation);
        }
    }

    public async Task LogManyAsync(IEnumerable<KyrolusElasticAuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvents);

        var eventList = auditEvents.ToList();
        if (eventList.Count == 0) return;

        var grouped = eventList.GroupBy(e => GetIndexName(e.Timestamp));

        foreach (var group in grouped)
        {
            var indexName = group.Key;
            var response = await _client.BulkAsync(b => b
                .Index(indexName)
                .IndexMany(group, (descriptor, ev) => descriptor.Id(ev.Id)),
                cancellationToken);

            if (!response.IsValidResponse || response.Errors)
            {
                _logger?.LogError("Bulk audit logging partially failed for index '{Index}': {Error}", indexName, response.DebugInformation);
            }
        }
    }

    private string GetIndexName(DateTimeOffset timestamp)
    {
        var prefix = _options.IndexPrefix ?? string.Empty;
        var suffix = _options.IndexSuffix ?? string.Empty;
        return $"{prefix}audit-logs-{timestamp:yyyy-MM}{suffix}".ToLowerInvariant();
    }
}
