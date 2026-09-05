namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Represents a route mapping in the API gateway.
/// </summary>
public sealed record KyrolusGatewayRoute
{
    public required string RouteId { get; init; }
    public required string ClusterId { get; init; }
    public required KyrolusGatewayRouteMatch Match { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
