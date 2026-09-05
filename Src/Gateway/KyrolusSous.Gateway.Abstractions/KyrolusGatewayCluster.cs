namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Represents a cluster of backend destination endpoints and its load balancing policy.
/// </summary>
public sealed record KyrolusGatewayCluster
{
    public required string ClusterId { get; init; }
    public required IReadOnlyDictionary<string, KyrolusGatewayDestination> Destinations { get; init; }
    public string? LoadBalancingPolicy { get; init; }
}
