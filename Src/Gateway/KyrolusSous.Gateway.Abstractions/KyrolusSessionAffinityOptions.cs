namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Configuration options for session affinity (sticky sessions) in a service cluster.
/// Binds requests from the same client/session to the same destination replica.
/// </summary>
public sealed record KyrolusSessionAffinityOptions
{
    /// <summary>
    /// Gets or sets whether session affinity is enabled for this cluster.
    /// Defaults to <c>true</c> when configured.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the affinity mechanism policy name (e.g., <c>"Cookie"</c> or <c>"CustomHeader"</c>).
    /// </summary>
    public KyrolusSessionAffinityPolicy? Policy { get; init; } = KyrolusSessionAffinityPolicy.Cookie;

    /// <summary>
    /// Gets or sets the strategy to use if the affinitized destination becomes unavailable (e.g., <c>"Redistribute"</c>).
    /// </summary>
    public KyrolusSessionAffinityFailurePolicy? FailurePolicy { get; init; } = KyrolusSessionAffinityFailurePolicy.Redistribute;

    /// <summary>
    /// Gets or sets the name of the cookie or header used to store the affinity token.
    /// Defaults to <c>".KyrolusGateway.Affinity"</c>.
    /// </summary>
    public string? AffinityKeyName { get; init; } = ".KyrolusGateway.Affinity";

    /// <summary>
    /// Gets or sets the hardened security options for the session affinity cookie.
    /// </summary>
    public KyrolusSessionAffinityCookieOptions? Cookie { get; init; }
}
