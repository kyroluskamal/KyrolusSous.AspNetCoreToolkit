namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Supported load balancing algorithms for distributing traffic among cluster destinations.
/// </summary>
public enum KyrolusLoadBalancingPolicy
{
    RoundRobin,
    LeastRequests,
    Random,
    PowerOfTwoChoices,
    Custom
}
