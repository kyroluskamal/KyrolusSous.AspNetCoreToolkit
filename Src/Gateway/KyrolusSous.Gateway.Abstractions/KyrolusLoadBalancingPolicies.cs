namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Standard load balancing policy names recognized by the reverse proxy.
/// </summary>
public static class KyrolusLoadBalancingPolicies
{
    public const string RoundRobin = "RoundRobin";
    public const string LeastRequests = "LeastRequests";
    public const string Random = "Random";
    public const string PowerOfTwoChoices = "PowerOfTwoChoices";
}
