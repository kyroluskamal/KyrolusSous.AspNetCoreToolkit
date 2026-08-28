namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Comprehensive configuration options for Kyrolus Elasticsearch messaging, security, ILM, and resilience.
/// </summary>
public class KyrolusElasticsearchOptions
{
    public string Url { get; set; } = "http://localhost:9200";

    public List<string> NodeUrls { get; set; } = [];

    public string? DefaultIndex { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? ApiKey { get; set; }

    public string? CertificateFingerprint { get; set; }

    public bool EnableDebugMode { get; set; } = false;

    public bool AutoCreateIndices { get; set; } = true;

    public bool UseIndexAliases { get; set; } = false;

    public int MaxRetries { get; set; } = 3;

    public int ConnectionTimeoutSeconds { get; set; } = 30;

    public int SlowQueryThresholdMs { get; set; } = 500;

    public int BulkBatchSize { get; set; } = 1000;

    public int MaxConcurrentBulkOperations { get; set; } = 4;

    public bool EnableHttpCompression { get; set; } = true;

    public string? IndexPrefix { get; set; }

    public string? IndexSuffix { get; set; }

    public bool EnableMultiTenancy { get; set; } = false;

    public TenantIsolationMode TenantIsolationMode { get; set; } = TenantIsolationMode.IndexPerTenant;

    public override string ToString()
    {
        var target = NodeUrls.Count > 0 ? string.Join(",", NodeUrls) : Url;
        return $"Elasticsearch[Target={target}, IndexPrefix={IndexPrefix}, User={Username}, Password=***, ApiKey={(string.IsNullOrWhiteSpace(ApiKey) ? "None" : "***")}, Compression={EnableHttpCompression}]";
    }
}
