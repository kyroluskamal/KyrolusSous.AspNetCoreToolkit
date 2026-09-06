namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Represents a logical cluster of equivalent backend destination instances (microservice replicas)
/// that handle requests forwarded by the API Gateway under a configured load balancing policy.
/// </summary>
/// <remarks>
/// <para>
/// <b>Concept:</b><br/>
/// In microservice and reverse proxy architectures, a <b>Cluster</b> groups multiple physical or containerized replicas of the same service.
/// For example, the <c>"orders-cluster"</c> may have 3 instances running in Kubernetes or on separate internal servers.
/// When an inbound request matches an associated route, the reverse proxy selects an available destination from this cluster using
/// the specified <see cref="LoadBalancingPolicy"/>.
/// </para>
/// <para>
/// <b>Destinations vs. Routes:</b><br/>
/// A cluster does not know about URL paths (like <c>/api/orders</c>). Paths belong to <see cref="KyrolusGatewayRoute"/>.
/// A cluster only knows about the backend endpoints (<see cref="Destinations"/>) and how to balance traffic between them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var cluster = new KyrolusGatewayCluster
/// {
///     ClusterId = "invoices-cluster",
///     LoadBalancingPolicy = KyrolusLoadBalancingPolicies.RoundRobin,
///     Destinations = new Dictionary&lt;string, KyrolusGatewayDestination&gt;
///     {
///         ["srv1"] = new KyrolusGatewayDestination("http://10.0.1.10:5000"),
///         ["srv2"] = new KyrolusGatewayDestination("http://10.0.1.11:5000")
///     }
/// };
/// </code>
/// </example>
public sealed record KyrolusGatewayCluster
{
    /// <summary>
    /// Gets the unique identifier for this cluster (e.g., <c>"orders-cluster"</c>, <c>"catalog-cluster"</c>).
    /// Routes reference this identifier to direct their traffic to this cluster.
    /// </summary>
    public required string ClusterId { get; init; }

    /// <summary>
    /// Gets the dictionary of available backend destination replicas, keyed by a unique destination identifier (e.g., <c>"node1"</c>, <c>"node2"</c>).
    /// </summary>
    public required IReadOnlyDictionary<string, KyrolusGatewayDestination> Destinations { get; init; }

    /// <summary>
    /// Gets the load balancing algorithm policy name used to distribute requests among destinations.
    /// Recommended values are available in <see cref="KyrolusLoadBalancingPolicies"/> (e.g. RoundRobin, LeastRequests, Random, PowerOfTwoChoices).
    /// </summary>
    public string? LoadBalancingPolicy { get; init; }

    /// <summary>
    /// Gets optional health check probing and observation options for the cluster destinations.
    /// </summary>
    public KyrolusHealthCheckOptions? HealthCheck { get; init; }

    /// <summary>
    /// Gets optional session affinity (sticky sessions) configuration for the cluster.
    /// </summary>
    public KyrolusSessionAffinityOptions? SessionAffinity { get; init; }

    /// <summary>
    /// Gets the HTTP activity/request timeout duration when communicating with backend destinations.
    /// Defends against hung connections, thread pool starvation, and cascading microservice failures.
    /// </summary>
    public TimeSpan? HttpRequestTimeout { get; init; }

    /// <summary>
    /// Gets optional HTTP client configuration for outbound calls to backend cluster destinations (e.g. SSL bypass, connection limits).
    /// </summary>
    public KyrolusHttpClientOptions? HttpClient { get; init; }

    /// <summary>
    /// Gets a value indicating whether responses from backend destinations should be buffered before delivery to the client.
    /// Set to <c>false</c> for real-time streaming, WebSockets, Server-Sent Events (SSE), and large file downloads.
    /// </summary>
    public bool? AllowResponseBuffering { get; init; }
}
