namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Criteria used to match an inbound HTTP request to a route.
/// </summary>
public sealed record KyrolusGatewayRouteMatch
{
    public required string Path { get; init; }
    public IReadOnlyList<string>? Methods { get; init; }
    public IReadOnlyList<string>? Hosts { get; init; }
}
