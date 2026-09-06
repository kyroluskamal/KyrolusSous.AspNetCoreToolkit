namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed active health check policy algorithm identifier.
/// Controls the algorithm used to evaluate active probe results for cluster destinations.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Active Health Check Algorithm:</strong><br/>
/// Determines how probe successes and failures are aggregated to decide when a destination replica
/// transitions between <em>Healthy</em> and <em>Unhealthy</em> states.
/// </para>
/// <para>
/// Standard policies are exposed as static properties on this struct (e.g. <see cref="ConsecutiveFailures"/>).
/// For custom policies registered in the YARP pipeline, use <see cref="Custom(string)"/>.
/// </para>
/// </remarks>
public readonly record struct KyrolusActiveHealthCheckPolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Active health check policy that marks a destination <em>Unhealthy</em> after N consecutive probe failures,
    /// and restores it to <em>Healthy</em> after the first successful probe.
    /// </summary>
    public static KyrolusActiveHealthCheckPolicy ConsecutiveFailures { get; } = new(KyrolusHealthCheckPolicies.ConsecutiveFailures);

    /// <summary>
    /// Gets the raw string policy name.
    /// </summary>
    public string Value => _value ?? KyrolusHealthCheckPolicies.ConsecutiveFailures;

    private KyrolusActiveHealthCheckPolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom active health check policy with the specified policy name.
    /// </summary>
    /// <param name="name">The custom policy name registered in the reverse proxy pipeline.</param>
    /// <returns>A new <see cref="KyrolusActiveHealthCheckPolicy"/> instance.</returns>
    public static KyrolusActiveHealthCheckPolicy Custom(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(name.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusActiveHealthCheckPolicy"/> from a raw string value, matching standard policies or creating a custom one.
    /// </summary>
    /// <param name="value">The policy name string, or <see langword="null"/>.</param>
    /// <returns>The resolved policy, or <see langword="null"/> if <paramref name="value"/> is null or empty.</returns>
    public static KyrolusActiveHealthCheckPolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return string.Equals(trimmed, KyrolusHealthCheckPolicies.ConsecutiveFailures, StringComparison.OrdinalIgnoreCase)
            ? ConsecutiveFailures
            : Custom(trimmed);
    }

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusActiveHealthCheckPolicy"/> instance to its underlying <see cref="string"/> representation.
    /// </summary>
    /// <param name="policy">The policy instance to convert.</param>
    /// <returns>The raw string policy name (e.g. <c>"ConsecutiveFailures"</c>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Why this exists:</strong><br/>
    /// Reverse proxy configurations and monitoring frameworks natively consume string policy names.
    /// This operator allows passing a strongly-typed <see cref="KyrolusActiveHealthCheckPolicy"/>
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
    /// var policy = KyrolusActiveHealthCheckPolicy.ConsecutiveFailures;
    /// string policyName = policy; // Automatically "ConsecutiveFailures"
    ///
    /// // 2. Passing to a method expecting string:
    /// void RegisterProbePolicy(string name) { ... }
    /// RegisterProbePolicy(policy); // Passed seamlessly without .ToString()
    /// </code>
    /// </example>
    public static implicit operator string(KyrolusActiveHealthCheckPolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a raw <see cref="string"/> policy name into a strongly-typed <see cref="KyrolusActiveHealthCheckPolicy"/>.
    /// </summary>
    /// <param name="value">The raw policy name string (e.g. from configuration or a string literal).</param>
    /// <returns>
    /// The matching strongly-typed <see cref="KyrolusActiveHealthCheckPolicy"/> if recognized,
    /// a custom policy if unrecognized, or <see cref="ConsecutiveFailures"/> if null or whitespace.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Why this exists:</strong><br/>
    /// Allows seamless binding from <c>IConfiguration</c> (<c>appsettings.json</c>) or string variables
    /// without requiring explicit calls to <see cref="From(string?)"/>.
    /// </para>
    /// <para>
    /// <strong>Matching Behavior:</strong><br/>
    /// Performs case-insensitive matching against standard policies (<c>"consecutivefailures"</c> -> <see cref="ConsecutiveFailures"/>).
    /// Custom values are wrapped via <see cref="Custom(string)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 1. Binding from configuration section:
    /// var activeOptions = new KyrolusActiveHealthCheckOptions
    /// {
    ///     Policy = configuration["ReverseProxy:Clusters:auth:ActiveHealthCheck:Policy"]
    /// };
    ///
    /// // 2. Passing a custom policy name directly as string:
    /// KyrolusActiveHealthCheckPolicy custom = "MyCustomLatencyProbePolicy";
    /// </code>
    /// </example>
    public static implicit operator KyrolusActiveHealthCheckPolicy(string value) => From(value) ?? ConsecutiveFailures;

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;
}