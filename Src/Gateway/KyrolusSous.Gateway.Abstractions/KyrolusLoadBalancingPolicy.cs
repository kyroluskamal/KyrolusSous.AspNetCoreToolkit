namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed load balancing policy algorithm identifier.
/// Controls how inbound requests are distributed across available backend destination replicas in a cluster.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Standard Load Balancing Algorithms:</strong><br/>
/// <list type="bullet">
///   <item><description><see cref="RoundRobin"/> (default) — Distributes requests sequentially across all healthy destinations.</description></item>
///   <item><description><see cref="LeastRequests"/> — Routes each request to the destination currently handling the fewest concurrent requests.</description></item>
///   <item><description><see cref="Random"/> — Picks a destination uniformly at random.</description></item>
///   <item><description><see cref="PowerOfTwoChoices"/> — Selects two random destinations and picks the one with fewer active requests.</description></item>
/// </list>
/// </para>
/// </remarks>
public readonly record struct KyrolusLoadBalancingPolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Sequential load balancing algorithm. Cycles through available destinations in order.
    /// Recommended default for homogeneous, stateless services with uniform request durations.
    /// </summary>
    public static KyrolusLoadBalancingPolicy RoundRobin { get; } = new(KyrolusLoadBalancingPolicies.RoundRobin);

    /// <summary>
    /// Dynamic load balancing algorithm that routes to the destination with the fewest currently active requests.
    /// Ideal for workloads with varying response times (e.g., file processing, reports, long-running queries).
    /// </summary>
    public static KyrolusLoadBalancingPolicy LeastRequests { get; } = new(KyrolusLoadBalancingPolicies.LeastRequests);

    /// <summary>
    /// Uniformly selects a destination at random for each incoming request.
    /// Simple and stateless, effective with large numbers of requests.
    /// </summary>
    public static KyrolusLoadBalancingPolicy Random { get; } = new(KyrolusLoadBalancingPolicies.Random);

    /// <summary>
    /// Picks two destinations at random and selects the one with fewer active connections.
    /// Combines the speed of random selection with the load-smoothing benefits of least requests, avoiding the herd effect.
    /// </summary>
    public static KyrolusLoadBalancingPolicy PowerOfTwoChoices { get; } = new(KyrolusLoadBalancingPolicies.PowerOfTwoChoices);
    private static readonly Dictionary<string, KyrolusLoadBalancingPolicy> KnownPolicies =
                  new(StringComparer.OrdinalIgnoreCase)
                  {
                      [KyrolusLoadBalancingPolicies.RoundRobin] = RoundRobin,
                      [KyrolusLoadBalancingPolicies.LeastRequests] = LeastRequests,
                      [KyrolusLoadBalancingPolicies.Random] = Random,
                      [KyrolusLoadBalancingPolicies.PowerOfTwoChoices] = PowerOfTwoChoices
                  };
    /// <summary>
    /// Gets the raw string policy name.
    /// </summary>
    public string Value => _value ?? KnownPolicies.First().Key; // Fallback to first known policy if somehow _value is null (should not happen)


    private KyrolusLoadBalancingPolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom load balancing policy with the specified policy name.
    /// </summary>
    /// <param name="name">The custom policy name registered in the reverse proxy pipeline.</param>
    /// <returns>A new <see cref="KyrolusLoadBalancingPolicy"/> instance.</returns>
    public static KyrolusLoadBalancingPolicy Custom(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(name.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusLoadBalancingPolicy"/> from a raw string value, matching standard policies or creating a custom one.
    /// </summary>
    /// <param name="value">The policy name string, or <see langword="null"/>.</param>
    /// <returns>The resolved policy, or <see langword="null"/> if <paramref name="value"/> is null or empty.</returns>
    public static KyrolusLoadBalancingPolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        return KnownPolicies.TryGetValue(trimmed, out var policy)
            ? policy : Custom(trimmed);
    }

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusLoadBalancingPolicy"/> instance to its underlying <see cref="string"/> representation.
    /// </summary>
    /// <param name="policy">The policy instance to convert.</param>
    /// <returns>The raw string policy name (e.g. <c>"RoundRobin"</c>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Why this exists:</strong><br/>
    /// Reverse proxy routing engines and load balancer factories identify algorithms by string names.
    /// This operator allows passing a strongly-typed <see cref="KyrolusLoadBalancingPolicy"/>
    /// directly into any method, log statement, or configuration object that accepts a <see cref="string"/>,
    /// without calling <c>.Value</c> or <c>.ToString()</c>.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong><br/>
    /// Because this is a <see langword="readonly record struct"/>, the conversion executes without any heap allocations.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 1. Assigning directly to a string variable:
    /// var policy = KyrolusLoadBalancingPolicy.LeastRequests;
    /// string policyName = policy; // Automatically "LeastRequests"
    ///
    /// // 2. Passing to a method expecting string:
    /// void ConfigureClusterLoadBalancer(string name) { ... }
    /// ConfigureClusterLoadBalancer(policy); // Passed seamlessly without .ToString()
    /// </code>
    /// </example>
    public static implicit operator string(KyrolusLoadBalancingPolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a raw <see cref="string"/> policy name into a strongly-typed <see cref="KyrolusLoadBalancingPolicy"/>.
    /// </summary>
    /// <param name="value">The raw policy name string (e.g. from configuration or a string literal).</param>
    /// <returns>
    /// The matching strongly-typed <see cref="KyrolusLoadBalancingPolicy"/> if recognized,
    /// a custom policy if unrecognized, or <see cref="RoundRobin"/> if null or whitespace.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Why this exists:</strong><br/>
    /// Allows seamless binding from <c>IConfiguration</c> (<c>appsettings.json</c>) or string constants
    /// without requiring explicit calls to <see cref="From(string?)"/>.
    /// </para>
    /// <para>
    /// <strong>Matching Behavior:</strong><br/>
    /// Performs case-insensitive matching against standard policies (<c>"roundrobin"</c>, <c>"leastrequests"</c>, <c>"random"</c>, <c>"poweroftwochoices"</c>).
    /// Custom values are wrapped via <see cref="Custom(string)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 1. Binding from configuration section:
    /// var cluster = new KyrolusGatewayCluster
    /// {
    ///     ClusterId = "orders-cluster",
    ///     Destinations = destinations,
    ///     LoadBalancingPolicy = configuration["ReverseProxy:Clusters:orders:LoadBalancingPolicy"]
    /// };
    ///
    /// // 2. Passing a custom registered load balancing policy name directly as string:
    /// KyrolusLoadBalancingPolicy custom = "MyCustomConsistentHashingPolicy";
    /// </code>
    /// </example>
    public static implicit operator KyrolusLoadBalancingPolicy(string value) => From(value) ?? RoundRobin;

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;
}