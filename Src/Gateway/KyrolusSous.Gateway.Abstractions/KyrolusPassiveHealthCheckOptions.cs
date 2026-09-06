namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Controls how the gateway watches real production traffic for signs that a destination is failing,
/// without sending any dedicated probe requests.
/// </summary>
/// <remarks>
/// <para>
/// <strong>What is Passive Health Checking?</strong>
/// </para>
/// <para>
/// Passive health checking is completely transparent. The gateway does not send any extra HTTP
/// calls to your backend services. Instead, it piggybacks on every real request it forwards,
/// silently recording the outcome:
/// </para>
/// <list type="bullet">
///   <item><description>Success: the destination replied with 1xx–4xx (business logic errors are still "transport success").</description></item>
///   <item><description>Failure: transport-level errors — connection refused, TCP timeout, TLS handshake failure, or 5xx from the server.</description></item>
/// </list>
/// <para>
/// When a destination accumulates enough failures (as determined by <see cref="Policy"/>), the
/// gateway quarantines it: it is marked <em>Unhealthy</em> and no new requests are routed to it.
/// After <see cref="ReactivationPeriod"/> elapses, the destination is automatically reactivated
/// (put back to <em>Unknown</em>) so the gateway can try it again. If it fails again immediately,
/// it is quarantined again for another <see cref="ReactivationPeriod"/>.
/// </para>
/// <para>
/// <strong>Key difference from Active health checking:</strong>
/// Passive detection only fires when users are actually sending requests. If a backend pod crashes
/// silently at 2:00 AM with no traffic, passive health checking will not know until the first
/// request hits it in the morning. For zero-traffic periods, combine with Active health checking.
/// </para>
/// <para>
/// <strong>Advantages of passive-only mode:</strong>
/// Zero additional load on backend services — this is critical when your destinations are
/// external third-party APIs that charge per request (payment gateways, SMS providers, maps APIs)
/// or enforce strict rate limits where a probe request might consume quota.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Use case 1 — Third-party payment API (charge-per-call, no probe traffic acceptable):</strong></para>
/// <para>
/// Stripe, PayPal, and similar APIs charge per API call. You cannot afford to send periodic
/// probe requests just to check availability. Passive monitoring watches real payment calls
/// and quarantines the destination if it starts returning 5xx errors or timing out.
/// </para>
/// <code>
/// new KyrolusPassiveHealthCheckOptions
/// {
///     Enabled            = true,
///     Policy             = KyrolusHealthCheckPolicies.TransportFailureRate,
///     ReactivationPeriod = TimeSpan.FromSeconds(60),  // wait 60s before retry
/// }
/// </code>
/// <para><strong>Use case 2 — Detecting memory-leak-induced degradation:</strong></para>
/// <para>
/// A backend service has a memory leak. It passes all active health probes (the /healthz endpoint
/// is simple and always returns 200) but gradually starts returning 503 responses to real user
/// requests as memory fills up. Passive monitoring catches this by tracking the 503 rate and
/// quarantines the replica before it fully crashes.
/// </para>
/// <code>
/// new KyrolusPassiveHealthCheckOptions
/// {
///     Enabled            = true,
///     Policy             = KyrolusHealthCheckPolicies.TransportFailureRate,
///     ReactivationPeriod = TimeSpan.FromSeconds(30),
/// }
/// </code>
/// </example>
public sealed record KyrolusPassiveHealthCheckOptions
{
    /// <summary>
    /// Whether the gateway should silently monitor real forwarded requests and quarantine destinations
    /// that exceed the error threshold configured in <see cref="Policy"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, the gateway never automatically quarantines a destination based
    /// on runtime error rates. All destinations remain in whatever health state they were assigned
    /// (e.g., by active probing, or <em>Unknown</em> if both are disabled).
    /// </remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The algorithm that decides when a destination has accumulated enough real-traffic failures
    /// to be quarantined.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The built-in YARP policy is <c>"TransportFailureRate"</c> (see
    /// <see cref="KyrolusHealthCheckPolicies.TransportFailureRate"/>). Under this policy the gateway
    /// tracks a rolling window of forwarded requests and measures the ratio of transport failures
    /// (5xx, connection errors, timeouts) to total requests. When the failure rate exceeds the
    /// configured threshold (default: 30% of the last 10 requests), the destination is marked
    /// <em>Unhealthy</em> and quarantined.
    /// </para>
    /// <para>
    /// The threshold and window size are configured separately on the YARP health check options
    /// in <c>appsettings.json</c>, not here. This property only selects which algorithm to activate.
    /// </para>
    /// <para>
    /// Defaults to <c>"TransportFailureRate"</c> when not specified.
    /// </para>
    /// </remarks>
    public KyrolusPassiveHealthCheckPolicy? Policy { get; init; } = KyrolusPassiveHealthCheckPolicy.TransportFailureRate;

    /// <summary>
    /// How long a quarantined destination waits before being automatically reactivated and retried.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a destination is marked <em>Unhealthy</em> by passive monitoring, all new requests
    /// bypass it. After this period elapses, the destination is put back to the <em>Unknown</em>
    /// state and the gateway begins forwarding requests to it again. If it fails immediately, it
    /// is quarantined for another <see cref="ReactivationPeriod"/>.
    /// </para>
    /// <para>
    /// This exponential-backoff-like behavior (quarantine → test → quarantine again if bad) gives
    /// a crashed destination time to restart and recover without being hammered by a flood of
    /// requests the moment it comes back online.
    /// </para>
    /// <para>
    /// Tune this based on your service's expected restart/recovery time:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>10–30s</c>: fast-starting services (stateless APIs that boot in seconds).</description></item>
    ///   <item><description><c>60–120s</c>: services that run heavy startup tasks (DB migrations, cache warming).</description></item>
    /// </list>
    /// <para>
    /// Defaults to <c>30 seconds</c> when not specified.
    /// </para>
    /// </remarks>
    public TimeSpan? ReactivationPeriod { get; init; } = TimeSpan.FromSeconds(30);
}
