namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Provides strongly-typed constants for the <em>health check algorithm</em> policies used by
/// active and passive destination health monitoring in a cluster.
/// </summary>
/// <remarks>
/// <para>
/// These constants are passed to <see cref="KyrolusActiveHealthCheckOptions.Policy"/> and
/// <see cref="KyrolusPassiveHealthCheckOptions.Policy"/> to select the algorithm the gateway
/// uses when deciding whether a destination should be marked <em>Unhealthy</em>.
/// </para>
/// <para>
/// For the policy that controls <em>which destinations receive traffic</em> based on their
/// health state, see <see cref="KyrolusAvailableDestinationsPolicies"/>.
/// </para>
/// </remarks>
public static class KyrolusHealthCheckPolicies
{
    /// <summary>
    /// Active health check policy that marks a destination <em>Unhealthy</em> after N
    /// consecutive probe failures, and restores it to <em>Healthy</em> after the first
    /// successful probe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the standard YARP built-in active health check policy. The gateway counts how
    /// many back-to-back probe requests have failed for a given destination. Once the count
    /// reaches the configured threshold (default: 2 consecutive failures), the destination is
    /// quarantined. A single successful probe immediately clears the failure counter and
    /// restores the destination to <em>Healthy</em>.
    /// </para>
    /// <para>
    /// <strong>Why 2 consecutive failures and not 1?</strong>
    /// A single probe failure is often caused by a transient network hiccup (packet loss, brief
    /// DNS blip) rather than a real outage. Requiring 2 in a row makes the policy resilient to
    /// noise without significantly delaying detection of a genuine crash.
    /// </para>
    /// <para>
    /// Use this constant with <see cref="KyrolusActiveHealthCheckOptions.Policy"/>.
    /// </para>
    /// </remarks>
    public const string ConsecutiveFailures = "ConsecutiveFailures";

    /// <summary>
    /// Passive health check policy that quarantines a destination when the ratio of
    /// transport-level failures (5xx responses, connection errors, timeouts) to total forwarded
    /// requests exceeds a configured threshold within a rolling observation window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ConsecutiveFailures"/>, this policy does not require N failures in a
    /// row — it tracks the overall failure <em>rate</em> across a sliding window of recent
    /// requests. For example, with a 30% threshold and a window of 10 requests: if 3 out of the
    /// last 10 real user requests to a destination returned 5xx or timed out, the destination is
    /// marked <em>Unhealthy</em> and quarantined for the duration of
    /// <see cref="KyrolusPassiveHealthCheckOptions.ReactivationPeriod"/>.
    /// </para>
    /// <para>
    /// <strong>What counts as a "transport failure" vs a "business error"?</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><strong>Transport failure (counted):</strong> TCP connection refused, TLS handshake error, request timeout, HTTP 5xx response (500, 502, 503, 504).</description></item>
    ///   <item><description><strong>Business error (not counted):</strong> HTTP 4xx responses (400, 401, 403, 404) — these mean the destination is alive and responding correctly; it is the client request that is invalid.</description></item>
    /// </list>
    /// <para>
    /// The threshold percentage and window size are configured on the YARP
    /// <c>PassiveHealthCheckConfig</c> in <c>appsettings.json</c>, not in this class.
    /// Use this constant with <see cref="KyrolusPassiveHealthCheckOptions.Policy"/>.
    /// </para>
    /// </remarks>
    public const string TransportFailureRate = "TransportFailureRate";
}

