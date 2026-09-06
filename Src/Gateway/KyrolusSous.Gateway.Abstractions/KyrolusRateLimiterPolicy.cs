namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed rate limiter policy identifier for gateway routing rules.
/// Defines the rate limiting strategy applied to inbound traffic matching this route.
/// </summary>
public readonly record struct KyrolusRateLimiterPolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Explicitly disables rate limiting for this route.
    /// Standard YARP reserved keyword (<c>"disable"</c>).
    /// </summary>
    public static KyrolusRateLimiterPolicy Disable { get; } = new("disable");

    /// <summary>
    /// Gets the raw string policy name.
    /// </summary>
    public string Value => _value ?? "disable";

    private KyrolusRateLimiterPolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom rate limiter policy with the specified policy name.
    /// </summary>
    /// <param name="policyName">The custom rate limiter policy name registered in ASP.NET Core Rate Limiting.</param>
    public static KyrolusRateLimiterPolicy Custom(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        return new(policyName.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusRateLimiterPolicy"/> from a raw string value.
    /// </summary>
    public static KyrolusRateLimiterPolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (string.Equals(trimmed, "disable", StringComparison.OrdinalIgnoreCase)) return Disable;
        return Custom(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusRateLimiterPolicy"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusRateLimiterPolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusRateLimiterPolicy"/>.
    /// </summary>
    public static implicit operator KyrolusRateLimiterPolicy(string? value) => From(value) ?? Custom(value ?? "disable");
}
