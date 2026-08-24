namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Configures resilience and automatic failure isolation settings for the Redis Circuit Breaker.
/// </summary>
/// <remarks>
/// <b>Real-World Disaster Scenario (Why Circuit Breaker is Essential):</b>
/// Suppose your Redis instance crashes or experiences severe network partition. 
/// Without a Circuit Breaker, every incoming HTTP request will wait 5 seconds for a Redis timeout before falling back to the database.
/// Under high traffic (e.g. 500 requests/sec), this ties up thousands of threads, exhausts the ASP.NET Core thread pool, 
/// and crashes the entire website!
/// <para>
/// <b>With Circuit Breaker Enabled:</b>
/// After 5 consecutive failures, the Circuit Breaker "Trips Open". For the next 10 seconds, all Redis calls fail instantly (0 ms), 
/// allowing your application to query the database directly and continue serving users with zero downtime.
/// </para>
/// </remarks>
public sealed class KyrolusRedisCircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the Circuit Breaker is enabled. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of consecutive Redis errors required to trip the circuit open. Defaults to 5.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// A single transient blip won't trip the circuit, but 5 consecutive errors confirm Redis is unreachable.
    /// </remarks>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the initial duration the circuit stays OPEN before allowing a trial probe request (Half-Open). Defaults to 10 seconds.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Gives the crashed Redis instance 10 seconds of breathing room to restart without being bombarded with traffic.
    /// </remarks>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum ceiling duration for exponential backoff while the circuit remains open. Defaults to 2 minutes.
    /// </summary>
    public TimeSpan? MaxOpenDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the exponential backoff multiplier applied if trial probe requests continue to fail. Defaults to 2.0.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// If Redis is still down after 10s, next wait is 20s, then 40s, up to <see cref="MaxOpenDuration"/>.
    /// </remarks>
    public double BackoffMultiplier { get; set; } = 2;

    /// <summary>
    /// Gets or sets the number of successful trial requests in the Half-Open state required to fully close and heal the circuit. Defaults to 1.
    /// </summary>
    public int HalfOpenSuccesses { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to throw a <see cref="KyrolusRedisCircuitOpenException"/> when the circuit is open, 
    /// or gracefully return null / default to allow seamless database fallback. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Leaving this as <c>false</c> enables seamless fallback where cache misses are treated as normal misses, 
    /// letting the user request continue to the database without throwing unhandled exceptions.
    /// </remarks>
    public bool ThrowOnOpen { get; set; }
}
