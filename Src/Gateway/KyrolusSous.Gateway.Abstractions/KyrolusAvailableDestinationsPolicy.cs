namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed available destinations policy identifier.
/// Controls which backend destination replicas the gateway considers available for routing based on their health state.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Destination Health States:</strong><br/>
/// Every destination is in one of four states: <em>Healthy</em>, <em>Unknown</em>, <em>Unspecified</em>, or <em>Unhealthy</em>.
/// This policy selects which non-unhealthy states are eligible to receive forwarded traffic.
/// </para>
/// <para>
/// Standard policies:
/// <list type="bullet">
///   <item><description><see cref="HealthyOrUnspecified"/> (default) — Accepts Healthy, Unknown, and Unspecified destinations. Recommended for general use.</description></item>
///   <item><description><see cref="HealthyAndUnknown"/> — Accepts Healthy and Unknown destinations only, rejecting unmonitored (Unspecified) replicas.</description></item>
/// </list>
/// </para>
/// </remarks>
public readonly record struct KyrolusAvailableDestinationsPolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Allows routing to destinations that are <em>Healthy</em>, in the <em>Unknown</em> state,
    /// or in the <em>Unspecified</em> state (health monitoring not configured).
    /// </summary>
    public static KyrolusAvailableDestinationsPolicy HealthyOrUnspecified { get; } = new(KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified);

    /// <summary>
    /// Allows routing to destinations that are <em>Healthy</em> or in the <em>Unknown</em> state.
    /// Rejects unmonitored (<em>Unspecified</em>) destinations.
    /// </summary>
    public static KyrolusAvailableDestinationsPolicy HealthyAndUnknown { get; } = new(KyrolusAvailableDestinationsPolicies.HealthyAndUnknown);
    private static readonly Dictionary<string, KyrolusAvailableDestinationsPolicy> KnownPolicies =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [KyrolusAvailableDestinationsPolicies.HealthyOrUnspecified] = HealthyOrUnspecified,
                [KyrolusAvailableDestinationsPolicies.HealthyAndUnknown] = HealthyAndUnknown
            };
    /// <summary>
    /// Gets the raw string policy name.
    /// </summary>
    public string Value => _value ?? KnownPolicies.First().Key; // Fallback to first known policy if somehow _value is null (should not happen)

    private KyrolusAvailableDestinationsPolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom available destinations policy with the specified policy name.
    /// </summary>
    /// <param name="name">The custom policy name registered in the reverse proxy pipeline.</param>
    /// <returns>A new <see cref="KyrolusAvailableDestinationsPolicy"/> instance.</returns>
    public static KyrolusAvailableDestinationsPolicy Custom(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(name.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusAvailableDestinationsPolicy"/> from a raw string value, matching standard policies or creating a custom one.
    /// </summary>
    /// <param name="value">The policy name string, or <see langword="null"/>.</param>
    /// <returns>The resolved policy, or <see langword="null"/> if <paramref name="value"/> is null or empty.</returns>
    public static KyrolusAvailableDestinationsPolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        return KnownPolicies.TryGetValue(trimmed, out var policy)
            ? policy : Custom(trimmed);
    }

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusAvailableDestinationsPolicy"/> instance to its underlying <see cref="string"/> representation.
    /// </summary>
    /// <param name="policy">The policy instance to convert.</param>
    /// <returns>The raw string policy name (e.g. <c>"HealthyOrUnspecified"</c>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Why this exists:</strong><br/>
    /// Under the hood, YARP and ASP.NET Core proxy components expect raw strings for configuration.
    /// This operator allows you to pass a strongly-typed <see cref="KyrolusAvailableDestinationsPolicy"/>
    /// directly into any method, logging call, or external library that expects a <see cref="string"/>,
    /// without manually typing <c>.Value</c> or <c>.ToString()</c>.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong><br/>
    /// Because this is a <see langword="readonly record struct"/>, this conversion is completely zero-allocation
    /// and involves no heap memory overhead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 1. Assigning directly to a string variable:
    /// var policy = KyrolusAvailableDestinationsPolicy.HealthyAndUnknown;
    /// string rawString = policy; // Automatically "HealthyAndUnknown"
    ///
    /// // 2. Passing directly into a method that expects a string:
    /// void ConfigureYarp(string yarpPolicyName) { ... }
    /// ConfigureYarp(policy); // Works seamlessly without .Value or .ToString()
    ///
    /// // 3. String interpolation and logging:
    /// logger.LogInformation("Active routing policy is: {Policy}", policy);
    /// </code>
    /// </example>
    public static implicit operator string(KyrolusAvailableDestinationsPolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a raw <see cref="string"/> policy name into a strongly-typed <see cref="KyrolusAvailableDestinationsPolicy"/>.
    /// </summary>
    /// <param name="value">The raw policy name string (e.g. from configuration or a string literal).</param>
    /// <returns>
    /// The matching strongly-typed <see cref="KyrolusAvailableDestinationsPolicy"/> if recognized,
    /// a custom policy if unrecognized, or <see cref="HealthyOrUnspecified"/> if null or whitespace.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Why this exists:</strong><br/>
    /// When reading policy settings from <c>appsettings.json</c>, environment variables, or databases,
    /// the values arrive as plain strings. This operator allows you to assign those strings directly to
    /// strongly-typed properties without calling <see cref="From(string?)"/> or doing manual parsing.
    /// </para>
    /// <para>
    /// <strong>Matching Behavior:</strong><br/>
    /// <list type="bullet">
    ///   <item><description>Case-insensitive match against standard policies (<c>"healthyandunknown"</c> -> <see cref="HealthyAndUnknown"/>).</description></item>
    ///   <item><description>Custom strings automatically wrap into a custom policy via <see cref="Custom(string)"/>.</description></item>
    ///   <item><description>Null or whitespace falls back safely to <see cref="HealthyOrUnspecified"/>.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 1. Assigning from a configuration section directly:
    /// var options = new KyrolusHealthCheckOptions
    /// {
    ///     // Configuration returns string, but the property is strongly-typed:
    ///     AvailableDestinationsPolicy = configuration["ReverseProxy:AvailableDestinationsPolicy"]
    /// };
    ///
    /// // 2. Assigning from a raw string literal (e.g. in test fixtures or scripts):
    /// KyrolusAvailableDestinationsPolicy policy = "HealthyAndUnknown";
    ///
    /// // 3. Passing a custom policy name directly as string:
    /// KyrolusAvailableDestinationsPolicy custom = "MyOrganizationStrictPolicy";
    /// </code>
    /// </example>
    public static implicit operator KyrolusAvailableDestinationsPolicy(string value) => From(value) ?? HealthyOrUnspecified;

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;
}