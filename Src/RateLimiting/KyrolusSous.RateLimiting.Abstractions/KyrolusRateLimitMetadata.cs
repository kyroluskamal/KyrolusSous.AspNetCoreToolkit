namespace KyrolusSous.RateLimiting.Abstractions;

/// <summary>
/// Standard implementation of <see cref="IKyrolusRateLimitMetadata"/> for endpoint metadata collections.
/// </summary>
/// <param name="PolicyName">Optional name of the rate limiter policy applied to this endpoint.</param>
/// <param name="IsExempt">Indicates whether the endpoint is exempt from rate limiting.</param>
/// <param name="SlaTier">Optional SLA criticality tier (e.g. "critical", "standard", "background").</param>
public sealed record KyrolusRateLimitMetadata(
    string? PolicyName = null,
    bool IsExempt = false,
    string? SlaTier = null) : IKyrolusRateLimitMetadata;
