namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed passive health check policy algorithm identifier.
/// Controls the algorithm used to evaluate real proxied traffic failures for cluster destinations.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Passive Health Check Algorithm:</strong><br/>
/// Observes runtime request outcomes (connection errors, timeouts, 5xx status codes) during client proxying
/// and temporarily quarantines destination replicas when failures exceed the policy's configured threshold.
/// </para>
/// <para>
/// Standard policies are exposed as static properties on this struct (e.g. <see cref="TransportFailureRate"/>).
/// For custom policies registered in the YARP pipeline, use <see cref="Custom(string)"/>.
/// </para>
/// </remarks>
public readonly record struct KyrolusPassiveHealthCheckPolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Passive health check policy that quarantines a destination when the ratio of transport-level failures
    /// (5xx responses, connection errors, timeouts) to total forwarded requests exceeds a configured threshold.
    /// </summary>
    public static KyrolusPassiveHealthCheckPolicy TransportFailureRate { get; } = new(KyrolusHealthCheckPolicies.TransportFailureRate);

    /// <summary>
    /// Gets the raw string policy name.
    /// </summary>
    public string Value => _value ?? KyrolusHealthCheckPolicies.TransportFailureRate;

    private KyrolusPassiveHealthCheckPolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom passive health check policy with the specified policy name.
    /// </summary>
    /// <param name="name">The custom policy name registered in the reverse proxy pipeline.</param>
    /// <returns>A new <see cref="KyrolusPassiveHealthCheckPolicy"/> instance.</returns>
    public static KyrolusPassiveHealthCheckPolicy Custom(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(name.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusPassiveHealthCheckPolicy"/> from a raw string value, matching standard policies or creating a custom one.
    /// </summary>
    /// <param name="value">The policy name string, or <see langword="null"/>.</param>
    /// <returns>The resolved policy, or <see langword="null"/> if <paramref name="value"/> is null or empty.</returns>
    public static KyrolusPassiveHealthCheckPolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))            return null;

        var trimmed = value.Trim();
        return string.Equals(trimmed, KyrolusHealthCheckPolicies.TransportFailureRate, StringComparison.OrdinalIgnoreCase)
            ? TransportFailureRate
            : Custom(trimmed);
    }

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusPassiveHealthCheckPolicy"/> instance to its underlying <see cref="string"/> representation.
    /// </summary>
    /// <param name="policy">The policy instance to convert.</param>
    /// <returns>The raw string policy name (e.g. <c>"TransportFailureRate"</c>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Why this exists:</strong><br/>
    /// Under the hood, YARP passive health check options consume raw string policy names.
    /// This operator allows you to pass a strongly-typed <see cref="KyrolusPassiveHealthCheckPolicy"/>
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
    /// var policy = KyrolusPassiveHealthCheckPolicy.TransportFailureRate;
    /// string policyName = policy; // Automatically "TransportFailureRate"
    ///
    /// // 2. Passing to a method expecting string:
    /// void SetYarpPassivePolicy(string name) { ... }
    /// SetYarpPassivePolicy(policy); // Passed seamlessly without .ToString()
    /// </code>
    /// </example>
    public static implicit operator string(KyrolusPassiveHealthCheckPolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a raw <see cref="string"/> policy name into a strongly-typed <see cref="KyrolusPassiveHealthCheckPolicy"/>.
    /// </summary>
    /// <param name="value">The raw policy name string (e.g. from configuration or a string literal).</param>
    /// <returns>
    /// The matching strongly-typed <see cref="KyrolusPassiveHealthCheckPolicy"/> if recognized,
    /// a custom policy if unrecognized, or <see cref="TransportFailureRate"/> if null or whitespace.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Why this exists:</strong><br/>
    /// Allows seamless binding from <c>IConfiguration</c> (<c>appsettings.json</c>) or string variables
    /// without requiring explicit calls to <see cref="From(string?)"/>.
    /// </para>
    /// <para>
    /// <strong>Matching Behavior:</strong><br/>
    /// Performs case-insensitive matching against standard policies (<c>"transportfailurerate"</c> -> <see cref="TransportFailureRate"/>).
    /// Custom values are wrapped via <see cref="Custom(string)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 1. Binding from configuration section:
    /// var passiveOptions = new KyrolusPassiveHealthCheckOptions
    /// {
    ///     Policy = configuration["ReverseProxy:Clusters:catalog:PassiveHealthCheck:Policy"]
    /// };
    ///
    /// // 2. Passing a custom policy name directly as string:
    /// KyrolusPassiveHealthCheckPolicy custom = "MyCustomConsecutive5xxPolicy";
    /// </code>
    /// </example>
    public static implicit operator KyrolusPassiveHealthCheckPolicy(string value) => From(value) ?? TransportFailureRate;

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;
}