namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Represents a target gateway destination address.
/// </summary>
/// <param name="Address">The destination URI address.</param>
public sealed record KyrolusGatewayDestination(string Address);
