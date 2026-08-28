namespace KyrolusSous.Gateway.Abstractions;

public sealed record KyrolusGatewayDestination(string Address);

public sealed record KyrolusGatewayCluster
{
    public required string ClusterId { get; init; }
    public required IReadOnlyDictionary<string, KyrolusGatewayDestination> Destinations { get; init; }
    public string? LoadBalancingPolicy { get; init; }
}

public sealed record KyrolusGatewayRouteMatch
{
    public required string Path { get; init; }
    public IReadOnlyList<string>? Methods { get; init; }
    public IReadOnlyList<string>? Hosts { get; init; }
}

public sealed record KyrolusGatewayRoute
{
    public required string RouteId { get; init; }
    public required string ClusterId { get; init; }
    public required KyrolusGatewayRouteMatch Match { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public interface IKyrolusDynamicRouteProvider
{
    Task<IReadOnlyList<KyrolusGatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusGatewayCluster>> GetClustersAsync(CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
