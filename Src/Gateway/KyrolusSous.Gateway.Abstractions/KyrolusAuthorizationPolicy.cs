namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed authorization policy identifier for gateway routing rules.
/// Controls authentication and authorization requirements enforced at the gateway edge.
/// </summary>
public readonly record struct KyrolusAuthorizationPolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Explicitly allows anonymous access to this route, bypassing any fallback or default authorization policies.
    /// Standard YARP reserved keyword (<c>"anonymous"</c>).
    /// </summary>
    public static KyrolusAuthorizationPolicy Anonymous { get; } = new("anonymous");

    /// <summary>
    /// Enforces the default ASP.NET Core authorization policy, requiring an authenticated user.
    /// Standard YARP reserved keyword (<c>"default"</c>).
    /// </summary>
    public static KyrolusAuthorizationPolicy Default { get; } = new("default");

    /// <summary>
    /// Gets the raw string policy name.
    /// </summary>
    public string Value => _value ?? "default";

    private KyrolusAuthorizationPolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom authorization policy with the specified policy name.
    /// </summary>
    /// <param name="policyName">The custom policy name registered in the ASP.NET Core authorization system.</param>
    public static KyrolusAuthorizationPolicy Custom(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        return new(policyName.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusAuthorizationPolicy"/> from a raw string value.
    /// </summary>
    public static KyrolusAuthorizationPolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (string.Equals(trimmed, "anonymous", StringComparison.OrdinalIgnoreCase)) return Anonymous;
        if (string.Equals(trimmed, "default", StringComparison.OrdinalIgnoreCase)) return Default;
        return Custom(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusAuthorizationPolicy"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusAuthorizationPolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusAuthorizationPolicy"/>.
    /// </summary>
    public static implicit operator KyrolusAuthorizationPolicy(string? value) => From(value) ?? Default;
}
