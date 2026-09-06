namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Controls how the gateway monitors the health of backend destination replicas and decides which ones
/// are safe to forward traffic to.
/// </summary>
/// <remarks>
/// <para>
/// A "destination" is a single backend replica — for example, one pod in a Kubernetes Deployment or one
/// VM behind a load balancer. In a real cluster you typically run 3–10 replicas. At any moment, one of
/// them can crash, run out of memory, or become overloaded. Without health checking, the gateway would
/// keep sending requests to it and users would get errors. With health checking configured here, the
/// gateway detects the problem and automatically stops routing to that replica until it recovers.
/// </para>
/// <para>
/// There are two complementary monitoring strategies you can enable independently or together:
/// </para>
/// <list type="bullet">
///   <item>
///     <term>Active (<see cref="Active"/>)</term>
///     <description>
///     The gateway itself periodically sends a dedicated HTTP probe request (like a ping) to each
///     destination <em>before</em> any real user traffic reaches it. Think of it as a doctor doing
///     routine check-ups on patients whether they feel sick or not. This catches problems proactively,
///     even when there is no user traffic (e.g., at night or during low-load periods).
///     </description>
///   </item>
///   <item>
///     <term>Passive (<see cref="Passive"/>)</term>
///     <description>
///     The gateway silently watches real production traffic flowing through it. When it notices that
///     responses from a specific destination are consistently failing (connection refused, 5xx errors,
///     timeouts), it marks that destination as unhealthy and quarantines it. Think of it as a nurse
///     standing beside the patient 24/7 and raising an alarm only when the patient actually shows symptoms.
///     This adds zero extra load but only detects failures once real users start experiencing them.
///     </description>
///   </item>
/// </list>
/// <para>
/// In most production systems you want <strong>both</strong> enabled:
/// Active gives you early detection before users are affected;
/// Passive catches subtle runtime degradations (memory leaks causing slow 5xx responses) that a
/// simple probe endpoint might miss.
/// </para>
/// <para>
/// Use <see cref="KyrolusHealthCheckPolicies"/> constants instead of raw strings to avoid typos.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Scenario 1 — Full monitoring (recommended for production):</strong></para>
/// <code>
/// new KyrolusHealthCheckOptions
/// {
///     Active = new KyrolusActiveHealthCheckOptions
///     {
///         Enabled  = true,
///         Path     = "/healthz",
///         Interval = TimeSpan.FromSeconds(10),
///         Timeout  = TimeSpan.FromSeconds(5),
///         Policy   = KyrolusHealthCheckPolicies.ConsecutiveFailures,
///     },
///     Passive = new KyrolusPassiveHealthCheckOptions
///     {
///         Enabled           = true,
///         Policy            = KyrolusHealthCheckPolicies.TransportFailureRate,
///         ReactivationPeriod = TimeSpan.FromSeconds(30),
///     },
///     AvailableDestinationsPolicy = KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified,
/// }
/// </code>
/// <para><strong>Scenario 2 — Passive-only (zero extra probe traffic, e.g., third-party APIs that charge per request):</strong></para>
/// <code>
/// new KyrolusHealthCheckOptions
/// {
///     Passive = new KyrolusPassiveHealthCheckOptions { Enabled = true },
///     AvailableDestinationsPolicy = KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified,
/// }
/// </code>
/// <para><strong>Scenario 3 — Active-only (internal services that need pre-traffic health gating):</strong></para>
/// <code>
/// new KyrolusHealthCheckOptions
/// {
///     Active = new KyrolusActiveHealthCheckOptions { Enabled = true, Path = "/ready" },
///     AvailableDestinationsPolicy = KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified,
/// }
/// </code>
/// </example>
public sealed record KyrolusHealthCheckOptions
{
    /// <summary>
    /// Configures proactive periodic probe requests the gateway sends to each destination to test
    /// its health <em>independently of real user traffic</em>.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, active probing is completely disabled and the gateway never
    /// sends any health probe HTTP requests to backend destinations. All destinations start in an
    /// "unknown" health state and whether they receive traffic depends on
    /// <see cref="AvailableDestinationsPolicy"/>.
    /// </remarks>
    public KyrolusActiveHealthCheckOptions? Active { get; init; }

    /// <summary>
    /// Configures silent observation of real production traffic to detect destinations that are
    /// responding with errors or dropping connections.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, passive health monitoring is completely disabled and the
    /// gateway never automatically quarantines a destination based on runtime error rates.
    /// </remarks>
    public KyrolusPassiveHealthCheckOptions? Passive { get; init; }

    /// <summary>
    /// Determines which destinations are considered eligible to receive forwarded requests based
    /// on their current health state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every destination is in one of four states at any moment: <em>Healthy</em>,
    /// <em>Unknown</em>, <em>Unspecified</em>, or <em>Unhealthy</em>. This property selects the
    /// policy that decides which of those states are treated as "available for routing".
    /// Unhealthy destinations are always excluded regardless of this setting.
    /// </para>
    /// <para>
    /// Use <see cref="KyrolusAvailableDestinationsPolicies"/> constants instead of raw string
    /// literals — each constant has detailed documentation explaining the exact semantics and
    /// use cases:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///     <see cref="KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified"/> (default) —
    ///     Healthy + Unknown + Unspecified destinations. Recommended for most systems.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     <see cref="KyrolusAvailableDestinationsPolicies.HealthyAndUnknown"/> —
    ///     Healthy + Unknown destinations only. Use when all destinations are monitored and
    ///     you want to exclude any unmonitored (Unspecified) destinations.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    public KyrolusAvailableDestinationsPolicy? AvailableDestinationsPolicy { get; init; } = KyrolusAvailableDestinationsPolicy.HealthyOrUnspecified;
}
