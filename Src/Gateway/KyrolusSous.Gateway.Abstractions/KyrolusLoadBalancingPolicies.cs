namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Provides standard, strongly-typed load balancing policy constants natively supported by the reverse proxy engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Load Balancing Policies Overview:</b><br/>
/// When a cluster has multiple backend destination replicas, the load balancer determines which replica receives each inbound request.
/// Using these constants prevents magic strings and eliminates runtime string allocation overhead:
/// <list type="bullet">
/// <item><description><b>RoundRobin</b>: Distributes requests sequentially across all healthy destinations (1 -&gt; 2 -&gt; 3 -&gt; 1).</description></item>
/// <item><description><b>LeastRequests</b>: Routes each request to the destination currently handling the fewest concurrent in-flight requests.</description></item>
/// <item><description><b>Random</b>: Picks a destination uniformly at random.</description></item>
/// <item><description><b>PowerOfTwoChoices</b>: Selects two random destinations and picks the one with fewer active requests, reducing lock contention.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Setting RoundRobin load balancing on a cluster builder:
/// cluster.WithLoadBalancing(KyrolusLoadBalancingPolicies.RoundRobin);
/// </code>
/// </example>
public static class KyrolusLoadBalancingPolicies
{
    /// <summary>
    /// Sequential load balancing algorithm. Cycles through available destinations in order.
    /// Recommended default for homogeneous, stateless services with uniform request durations.
    /// </summary>
    public const string RoundRobin = "RoundRobin";

    /// <summary>
    /// Dynamic load balancing algorithm that routes to the destination with the fewest currently active requests.
    /// Ideal for workloads with varying response times (e.g., file processing, reports, long-running queries).
    /// </summary>
    public const string LeastRequests = "LeastRequests";

    /// <summary>
    /// Uniformly selects a destination at random for each incoming request.
    /// Simple and stateless, effective with large numbers of requests.
    /// </summary>
    public const string Random = "Random";

    /// <summary>
    /// Picks two destinations at random and selects the one with fewer active connections.
    /// Combines the speed of random selection with the load-smoothing benefits of least requests, avoiding the herd effect.
    /// </summary>
    public const string PowerOfTwoChoices = "PowerOfTwoChoices";
}
