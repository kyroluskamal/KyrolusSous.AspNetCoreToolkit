namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Provides strongly-typed constants for the
/// <see cref="KyrolusHealthCheckOptions.AvailableDestinationsPolicy"/> setting, which controls
/// which backend destinations the gateway is allowed to route traffic to based on their current
/// health state.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Background — the four health states a destination can be in:</strong>
/// </para>
/// <list type="table">
///   <listheader>
///     <term>State</term>
///     <description>When it occurs</description>
///   </listheader>
///   <item>
///     <term><em>Healthy</em></term>
///     <description>
///     Active or passive health checking is configured AND the destination has passed its most
///     recent health evaluation (probe returned 2xx, or passive error rate is below threshold).
///     </description>
///   </item>
///   <item>
///     <term><em>Unknown</em></term>
///     <description>
///     Health checking IS configured for the cluster, but not enough results have been recorded
///     yet to make a verdict. This is the initial state of every destination on gateway startup,
///     before the first probe response arrives or before any real traffic has been observed.
///     </description>
///   </item>
///   <item>
///     <term><em>Unspecified</em></term>
///     <description>
///     Health checking is NOT configured at all for the cluster
///     (<see cref="KyrolusHealthCheckOptions.Active"/> and
///     <see cref="KyrolusHealthCheckOptions.Passive"/> are both <see langword="null"/>).
///     The gateway has no health data about this destination because it was never asked to collect any.
///     </description>
///   </item>
///   <item>
///     <term><em>Unhealthy</em></term>
///     <description>
///     Health checking IS configured AND the destination has been explicitly marked as failing
///     (failed consecutive probes, or passive error rate exceeded threshold). The destination is
///     quarantined and will not receive traffic regardless of which policy you choose here.
///     </description>
///   </item>
/// </list>
/// <para>
/// The policy you choose here answers the question:
/// <em>"When should a destination be considered available for routing if it has not been
/// explicitly confirmed healthy yet?"</em>
/// </para>
/// <para>
/// Use the constants in this class instead of raw string literals to avoid typos and make the
/// intent clear to future readers of your configuration code.
/// </para>
/// </remarks>
public static class KyrolusAvailableDestinationsPolicies
{
    /// <summary>
    /// Allows routing to destinations that are <em>Healthy</em> or in the <em>Unknown</em> state.
    /// Destinations in the <em>Unspecified</em> state (no health monitoring configured) are
    /// <strong>not</strong> allowed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>When to use this:</strong>
    /// You have a cluster where ALL destinations are under health monitoring
    /// (<see cref="KyrolusHealthCheckOptions.Active"/> or
    /// <see cref="KyrolusHealthCheckOptions.Passive"/> are configured), and you want to allow
    /// traffic to flow during the brief startup window before the first probe results arrive.
    /// </para>
    /// <para>
    /// <strong>Why "Unknown" is included:</strong>
    /// On gateway startup, every destination begins in the <em>Unknown</em> state. If you used
    /// a strict "Healthy-only" policy, the gateway would refuse ALL traffic until at least one
    /// probe had completed — causing a cold-start outage of up to one full
    /// <see cref="KyrolusActiveHealthCheckOptions.Interval"/>. Including <em>Unknown</em> avoids
    /// this gap while still ensuring that once probing is running, only healthy destinations serve traffic.
    /// </para>
    /// <para>
    /// <strong>Risk:</strong>
    /// During startup, a destination that is actually unhealthy (e.g., a pod that started but
    /// cannot connect to its database) will temporarily receive requests in the <em>Unknown</em>
    /// window until the first probe fails and marks it <em>Unhealthy</em>.
    /// </para>
    /// <para>
    /// <strong>Comparison with <see cref="HealthyOrUnspecified"/>:</strong>
    /// Both policies include <em>Unknown</em> destinations. The difference is that
    /// <see cref="HealthyAndUnknown"/> explicitly rejects destinations where monitoring is
    /// <em>Unspecified</em> (not configured), whereas <see cref="HealthyOrUnspecified"/> accepts them.
    /// If all your destinations always have health monitoring configured, both policies behave identically.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para><strong>Use case — Kubernetes rolling deployment with health probing:</strong></para>
    /// <para>
    /// You deploy 5 pods, all registered with active health probing. When a new pod starts,
    /// it enters <em>Unknown</em> state. You want it to accept traffic immediately (so the
    /// deployment does not stall), but as soon as it fails a probe it should be removed.
    /// </para>
    /// <code>
    /// new KyrolusHealthCheckOptions
    /// {
    ///     Active = new KyrolusActiveHealthCheckOptions { Enabled = true, Path = "/healthz" },
    ///     AvailableDestinationsPolicy = KyrolusAvailableDestinationsPolicies.HealthyAndUnknown,
    /// }
    /// </code>
    /// </example>
    public const string HealthyAndUnknown = "HealthyAndUnknown";

    /// <summary>
    /// Allows routing to destinations that are <em>Healthy</em>, in the <em>Unknown</em> state,
    /// or in the <em>Unspecified</em> state (health monitoring not configured for the cluster).
    /// This is the recommended default for most systems.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>When to use this:</strong>
    /// This is the safe default and covers the widest range of deployment scenarios:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <description>
    ///     <strong>No health monitoring at all</strong> — if both
    ///     <see cref="KyrolusHealthCheckOptions.Active"/> and
    ///     <see cref="KyrolusHealthCheckOptions.Passive"/> are <see langword="null"/>, all
    ///     destinations are <em>Unspecified</em>. This policy keeps them routable, giving you
    ///     full functionality even without health monitoring enabled.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     <strong>Health monitoring enabled</strong> — destinations transition through
    ///     <em>Unknown</em> to <em>Healthy</em> to <em>Unhealthy</em> as probes run. Unknown
    ///     destinations still receive traffic during the startup window (same as
    ///     <see cref="HealthyAndUnknown"/>).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     <strong>Mixed clusters</strong> — some destinations have monitoring configured and
    ///     others do not. The unmonitored destinations remain routable as <em>Unspecified</em>,
    ///     while the monitored ones follow their normal health cycle.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// <strong>When NOT to use this:</strong>
    /// If you need a strict guarantee that only destinations with a confirmed health check
    /// result receive production traffic, use <see cref="HealthyAndUnknown"/> (which at least
    /// rejects unmonitored destinations) or implement a custom Healthy-only policy
    /// (which also rejects <em>Unknown</em> destinations). For example: a payment processor
    /// cluster during a high-risk deployment where you would rather return 503 than send
    /// traffic to an untested replica.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para><strong>Use case 1 — Development or staging (no health probing configured):</strong></para>
    /// <para>
    /// You have a single backend destination and no health monitoring configured.
    /// HealthyOrUnspecified keeps it routable without needing to set up probes.
    /// </para>
    /// <code>
    /// new KyrolusHealthCheckOptions
    /// {
    ///     // No Active or Passive configured: destination state is Unspecified
    ///     AvailableDestinationsPolicy = KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified,
    /// }
    /// </code>
    /// <para><strong>Use case 2 — Production cluster with full monitoring (recommended):</strong></para>
    /// <code>
    /// new KyrolusHealthCheckOptions
    /// {
    ///     Active  = new KyrolusActiveHealthCheckOptions  { Enabled = true, Path = "/healthz" },
    ///     Passive = new KyrolusPassiveHealthCheckOptions { Enabled = true },
    ///     AvailableDestinationsPolicy = KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified,
    /// }
    /// </code>
    /// <para><strong>Use case 3 — Mixed cluster (some monitored, some legacy/unmonitored):</strong></para>
    /// <para>
    /// You have 3 internal replicas with active probing and 1 legacy VM that cannot expose a
    /// health endpoint. The legacy VM stays <em>Unspecified</em> (routable) while the
    /// monitored replicas follow their normal health lifecycle.
    /// </para>
    /// <code>
    /// new KyrolusHealthCheckOptions
    /// {
    ///     Active = new KyrolusActiveHealthCheckOptions { Enabled = true, Path = "/healthz" },
    ///     AvailableDestinationsPolicy = KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified,
    /// }
    /// </code>
    /// </example>
    public const string HealthyOrUnspecified = "HealthyOrUnspecified";
}