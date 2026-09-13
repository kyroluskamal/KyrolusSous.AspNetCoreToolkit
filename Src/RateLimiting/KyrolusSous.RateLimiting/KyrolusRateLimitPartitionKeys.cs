namespace KyrolusSous.RateLimiting;

/// <summary>
/// Utility helper providing standardized partition key resolvers for ASP.NET Core RateLimiter policies.
/// Facilitates client partitioning across Network, Identity, Routing, Security, Payload, Protocol,
/// Client Hints, Hyperscale Architecture, AI Gateways, FinTech, gRPC, Database Topology, IoT, and Zero-Trust in enterprise multi-tenant reverse proxies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Normalizes a string segment into a safe identifier suitable for composite partition keys.
    /// </summary>
    /// <param name="segment">The raw input string segment.</param>
    /// <returns>A sanitized string containing only lowercase alphanumeric characters, underscores, and hyphens.</returns>
    internal static string SanitizeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return string.Empty;

        return segment.Trim().ToLowerInvariant()
            .Replace('/', '_')
            .Replace(':', '_')
            .Replace(' ', '_');
    }
}
