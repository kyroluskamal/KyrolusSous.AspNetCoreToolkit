using Microsoft.AspNetCore.Http;

namespace KyrolusSous.RateLimiting;

/// <summary>
/// Database Topology, CQRS Primary vs Replica Intent, Sovereign Cloud Data Residency, and Tenant Physical Sharding partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    private static readonly string[] DefaultDbIntentHeaders =
    [
        "X-Database-Target",
        "X-DB-Intent",
        "X-Read-Replica",
        "X-Database-Route"
    ];

    /// <summary>
    /// Resolves a partition key based on CQRS database targeting intent (<c>"db_primary"</c> vs <c>"db_replica"</c>).
    /// Prevents high-throughput analytical and read-heavy queries from consuming write capacity on the primary master database instance.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="intentHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The database targeting intent partition key (<c>"db_primary"</c> or <c>"db_replica"</c>).</returns>
    public static string GetDatabaseReplicaIntentKey(HttpContext context, string[]? intentHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = intentHeaderCandidates is { Length: > 0 } ? intentHeaderCandidates : DefaultDbIntentHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (raw is "replica" or "secondary" or "follower" or "read" or "readonly" or "true" or "1")
                    return "db_replica";
                if (raw is "primary" or "master" or "leader" or "write" or "false" or "0")
                    return "db_primary";
            }
        }

        // Semantic fallback: safe HTTP methods target read replicas; mutations target primary
        return HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
            ? "db_replica"
            : "db_primary";
    }

    /// <summary>
    /// Resolves a compound partition key combining the database replica intent with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="intentHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{intentKey}:{clientKey}"</c>.</returns>
    public static string GetDatabaseReplicaIntentAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? intentHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var intentKey = GetDatabaseReplicaIntentKey(context, intentHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{intentKey}:{clientKey}";
    }

    private static readonly string[] DefaultResidencyHeaders =
    [
        "X-Data-Residency",
        "X-Data-Sovereignty",
        "X-Compliance-Zone",
        "X-Residency-Region"
    ];

    /// <summary>
    /// Resolves a partition key based on Sovereign Cloud data residency and legal compliance boundaries (e.g. <c>"residency_eu_gdpr"</c>, <c>"residency_us_hipaa"</c>, <c>"residency_sa_pdpl"</c>).
    /// Enforces traffic isolation per sovereign jurisdiction to prevent regional compliance quota spills.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="residencyHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The data residency partition key formatted as <c>"residency_{zone}"</c>, or <c>"residency_global"</c>.</returns>
    public static string GetDataResidencyKey(HttpContext context, string[]? residencyHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = residencyHeaderCandidates is { Length: > 0 } ? residencyHeaderCandidates : DefaultResidencyHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"residency_{SanitizeSegment(raw)}";
            }
        }

        return "residency_global";
    }

    /// <summary>
    /// Resolves a compound partition key combining the data residency zone with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="residencyHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{residencyKey}:{clientKey}"</c>.</returns>
    public static string GetDataResidencyAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? residencyHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var residencyKey = GetDataResidencyKey(context, residencyHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{residencyKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the physical database tenant shard identifier (<c>"dbshard_shard01"</c>, <c>"dbshard_shard02"</c>).
    /// Implements physical multi-tenant database isolation at the gateway layer, shielding healthy tenant database clusters from noisy neighbor spikes.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="shardHeader">The header name containing the physical shard ID. Defaults to <c>"X-Tenant-Shard"</c>.</param>
    /// <returns>The physical database shard partition key formatted as <c>"dbshard_{shard}"</c>, or <c>"dbshard_default"</c>.</returns>
    public static string GetTenantDatabaseShardKey(HttpContext context, string shardHeader = "X-Tenant-Shard")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(shardHeader);

        if (context.Request.Headers.TryGetValue(shardHeader, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            return $"dbshard_{SanitizeSegment(val.ToString().Trim())}";
        }

        if (context.Request.Headers.TryGetValue("X-DB-Shard", out var fallbackVal) && !string.IsNullOrWhiteSpace(fallbackVal))
        {
            return $"dbshard_{SanitizeSegment(fallbackVal.ToString().Trim())}";
        }

        return "dbshard_default";
    }

    /// <summary>
    /// Resolves a compound partition key combining the physical database shard with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="shardHeader">The header name containing the physical shard ID.</param>
    /// <returns>A compound key formatted as <c>"{dbShardKey}:{clientKey}"</c>.</returns>
    public static string GetTenantDatabaseShardAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string shardHeader = "X-Tenant-Shard")
    {
        ArgumentNullException.ThrowIfNull(context);

        var shardKey = GetTenantDatabaseShardKey(context, shardHeader);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{shardKey}:{clientKey}";
    }
}
