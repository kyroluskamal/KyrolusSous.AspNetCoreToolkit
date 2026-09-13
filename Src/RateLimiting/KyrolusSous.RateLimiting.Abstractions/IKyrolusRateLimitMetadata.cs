namespace KyrolusSous.RateLimiting.Abstractions;

/// <summary>
/// Endpoint metadata describing rate limiting behavior, SLA tiers, or exemption status.
/// </summary>
public interface IKyrolusRateLimitMetadata
{
    /// <summary>
    /// Gets the name of the rate limiter policy applied to this endpoint.
    /// </summary>
    string? PolicyName { get; }

    /// <summary>
    /// Gets a value indicating whether this endpoint is exempt from rate limiting.
    /// </summary>
    bool IsExempt { get; }

    /// <summary>
    /// Gets the optional SLA criticality tier (e.g. "critical", "standard", "background").
    /// </summary>
    string? SlaTier { get; }
}
