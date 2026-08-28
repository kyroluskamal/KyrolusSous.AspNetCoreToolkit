namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Health check implementation probing Elasticsearch cluster connectivity and cluster health status.
/// </summary>
public class KyrolusElasticsearchHealthCheck(
    ElasticsearchClient client,
    ILogger<KyrolusElasticsearchHealthCheck>? logger = null) : IHealthCheck
{
    private readonly ElasticsearchClient _client = client;
    private readonly ILogger<KyrolusElasticsearchHealthCheck>? _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PingAsync(cancellationToken);
            if (response.IsValidResponse)
            {
                return HealthCheckResult.Healthy("Elasticsearch cluster is reachable and healthy.");
            }

            _logger?.LogWarning("Elasticsearch health check ping failed: {Error}", response.DebugInformation);
            return HealthCheckResult.Unhealthy($"Elasticsearch ping failed: {response.ElasticsearchServerError?.Error.Reason ?? "Unknown error"}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Elasticsearch health check encountered an exception.");
            return HealthCheckResult.Unhealthy("Elasticsearch health check failed.", ex);
        }
    }
}

/// <summary>
/// Backward-compatibility alias for <see cref="KyrolusElasticsearchHealthCheck"/>.
/// </summary>
public class ElasticsearchHealthCheck(
    ElasticsearchClient client,
    ILogger<ElasticsearchHealthCheck>? logger = null)
    : KyrolusElasticsearchHealthCheck(client, logger is null ? null : null)
{
}
