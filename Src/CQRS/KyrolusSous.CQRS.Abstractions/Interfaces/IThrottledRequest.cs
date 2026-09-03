namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a query or command with concurrency throttling limits to prevent system overload.
/// </summary>
public interface IThrottledRequest : IKyrolusRequestBase
{
    /// <summary>
    /// Throttling partition key (e.g. "reports", "export", or tenant identifier). If null, request type name is used.
    /// </summary>
    string? ThrottleKey => null;

    /// <summary>
    /// Maximum concurrent executions allowed for this throttle key.
    /// </summary>
    int MaxConcurrentExecutions => 5;

    /// <summary>
    /// Maximum time to wait for a concurrency slot before throwing a timeout exception.
    /// </summary>
    TimeSpan ThrottleTimeout => TimeSpan.FromSeconds(30);
}
