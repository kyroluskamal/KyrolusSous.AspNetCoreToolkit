namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Controls the gateway's proactive background health probing for the destinations in a cluster.
/// </summary>
/// <remarks>
/// <para>
/// <strong>What is Active Health Checking?</strong>
/// </para>
/// <para>
/// Active health checking means the gateway itself acts like a monitoring agent. Every
/// <see cref="Interval"/> seconds, it sends a lightweight HTTP GET request to each destination
/// at the URL specified by <see cref="Path"/> (e.g., <c>GET https://replica-3:8080/healthz</c>).
/// If the response is a success (2xx), the destination is marked <em>Healthy</em>.
/// If the probe fails (connection refused, timeout, 5xx, or other error), the failure is recorded.
/// Once failures accumulate to the threshold configured in the chosen <see cref="Policy"/>,
/// the destination is marked <em>Unhealthy</em> and removed from the routing pool — no more
/// real user requests will reach it until it recovers and passes probes again.
/// </para>
/// <para>
/// <strong>Key difference from Passive health checking:</strong>
/// Active probing detects a crashed replica <em>even when there are no users online</em>.
/// For example, if one of your 5 backend pods crashes at 3:00 AM with zero traffic, active
/// health checking will notice within one probe interval and mark it unhealthy — so when the
/// morning rush hits, that dead pod is already excluded from the pool.
/// </para>
/// <para>
/// <strong>Trade-off:</strong>
/// Each destination receives one probe request per <see cref="Interval"/>. For a cluster of
/// 10 destinations with a 10-second interval, that is 1 probe/second of extra load. This is
/// usually negligible, but it can matter if you are calling an external third-party API that
/// charges per request or enforces strict rate limits.
/// </para>
/// <para>
/// Your backend service must expose a dedicated health endpoint (e.g., <c>/healthz</c> or
/// <c>/health/ready</c>) that the gateway can call. In ASP.NET Core this is typically added
/// via <c>app.MapHealthChecks("/healthz")</c>. The endpoint should return 200 when the service
/// is fully operational, and a non-2xx code (e.g., 503) when it is starting up, overloaded,
/// or shutting down.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Use case 1 — Kubernetes pod startup gating:</strong></para>
/// <para>
/// Kubernetes marks a pod as Running before the ASP.NET application inside is fully ready
/// (e.g., still running EF Core migrations or warming the cache). Active health checking with
/// a <c>/health/ready</c> endpoint ensures the gateway only routes to a pod after it explicitly
/// declares itself ready, preventing "502 Bad Gateway" during rolling deployments.
/// </para>
/// <code>
/// new KyrolusActiveHealthCheckOptions
/// {
///     Enabled  = true,
///     Path     = "/health/ready",   // readiness probe, not liveness
///     Interval = TimeSpan.FromSeconds(5),
///     Timeout  = TimeSpan.FromSeconds(3),
///     Policy   = KyrolusHealthCheckPolicies.ConsecutiveFailures,
/// }
/// </code>
/// <para><strong>Use case 2 — Canary deployment validation:</strong></para>
/// <para>
/// You deploy a new version as a single canary replica. Active health checking with a short
/// interval and tight timeout gives you fast automated feedback on whether the new version
/// starts healthy before you shift significant traffic to it.
/// </para>
/// <code>
/// new KyrolusActiveHealthCheckOptions
/// {
///     Enabled  = true,
///     Path     = "/healthz",
///     Interval = TimeSpan.FromSeconds(3),
///     Timeout  = TimeSpan.FromSeconds(2),
///     Policy   = KyrolusHealthCheckPolicies.ConsecutiveFailures,
/// }
/// </code>
/// </example>
public sealed record KyrolusActiveHealthCheckOptions
{
    /// <summary>
    /// Whether the gateway should send periodic health probe requests to each destination in this cluster.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, all probe requests are suppressed. No HTTP calls are made to
    /// backend destinations for the purpose of health testing. Destinations retain whatever health
    /// state they were last assigned (typically <em>Unknown</em> on startup).
    /// </remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// How often the gateway probes each destination. Shorter intervals detect failures faster;
    /// longer intervals reduce probe overhead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the time the gateway waits between the end of one probe and the start of the next
    /// for a given destination. For example, with <c>Interval = 10s</c> and <c>Timeout = 5s</c>:
    /// </para>
    /// <list type="number">
    ///   <item><description>Gateway sends probe at T=0.</description></item>
    ///   <item><description>Destination responds at T=3s → success recorded.</description></item>
    ///   <item><description>Gateway waits 10s → sends next probe at T=13s.</description></item>
    /// </list>
    /// <para>
    /// Typical values:
    /// <c>5–10s</c> for services that need fast failure detection (payment gateways, auth servers);
    /// <c>30–60s</c> for stable services where health rarely changes.
    /// </para>
    /// <para>
    /// Defaults to <c>10 seconds</c> when not specified.
    /// </para>
    /// </remarks>
    public TimeSpan? Interval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The maximum time the gateway waits for a health probe response before treating it as a failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If a probe request does not receive a response within this duration, the gateway cancels
    /// the request and records a probe failure — identical to receiving a 5xx error. This prevents
    /// an overloaded-but-alive destination from appearing healthy just because it is slow.
    /// </para>
    /// <para>
    /// Rule of thumb: <see cref="Timeout"/> should be significantly less than <see cref="Interval"/>
    /// to avoid probe queuing. Example: Interval=10s, Timeout=5s. Never set Timeout &gt;= Interval.
    /// </para>
    /// <para>
    /// Defaults to <c>5 seconds</c> when not specified.
    /// </para>
    /// </remarks>
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The algorithm that decides when a destination should be marked unhealthy based on accumulated probe results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The built-in YARP policy is <c>"ConsecutiveFailures"</c> (see
    /// <see cref="KyrolusHealthCheckPolicies.ConsecutiveFailures"/>). Under this policy a
    /// destination is marked <em>Unhealthy</em> after N consecutive probe failures, and
    /// marked <em>Healthy</em> again after the first successful probe.
    /// </para>
    /// <para>
    /// By default N=2 (two back-to-back probe failures). This single-failure tolerance prevents
    /// a transient network hiccup from unnecessarily ejecting a healthy destination from the pool.
    /// </para>
    /// <para>
    /// Defaults to <c>"ConsecutiveFailures"</c> when not specified.
    /// </para>
    /// </remarks>
    public KyrolusActiveHealthCheckPolicy? Policy { get; init; } = KyrolusActiveHealthCheckPolicy.ConsecutiveFailures;

    /// <summary>
    /// The HTTP URL path the gateway requests on each destination to check its health.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This path is appended to each destination's base address. For example, if a destination's
    /// address is <c>https://api-pod-3:8080</c> and <see cref="Path"/> is <c>/healthz</c>, the
    /// gateway sends <c>GET https://api-pod-3:8080/healthz</c>.
    /// </para>
    /// <para>
    /// Best practice: use a dedicated readiness endpoint that checks all critical dependencies
    /// (database connectivity, cache warm-up, feature flag loading) rather than a trivial
    /// <c>"I'm alive"</c> ping. A destination that is alive but cannot reach its database
    /// should return a non-2xx response so the gateway routes around it.
    /// </para>
    /// <para>
    /// Common conventions: <c>/health</c>, <c>/healthz</c>, <c>/health/ready</c>, <c>/ping</c>.
    /// ASP.NET Core: register via <c>app.MapHealthChecks("/healthz")</c>.
    /// </para>
    /// <para>
    /// Defaults to <c>"/health"</c> when not specified.
    /// </para>
    /// </remarks>
    public string? Path { get; init; } = "/health";
}
