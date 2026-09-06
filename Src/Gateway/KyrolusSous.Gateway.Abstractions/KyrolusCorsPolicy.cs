namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed Cross-Origin Resource Sharing (CORS) policy identifier for gateway routing rules.
/// Controls browser cross-origin preflight and request processing at the gateway perimeter.
/// </summary>
public readonly record struct KyrolusCorsPolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Applies the default CORS policy registered in the ASP.NET Core application.
    /// Standard YARP reserved keyword (<c>"default"</c>).
    /// </summary>
    public static KyrolusCorsPolicy Default { get; } = new("default");

    /// <summary>
    /// Explicitly disables CORS processing for this route.
    /// Standard YARP reserved keyword (<c>"disable"</c>).
    /// </summary>
    public static KyrolusCorsPolicy Disable { get; } = new("disable");

    /// <summary>
    /// Gets the raw string policy name.
    /// </summary>
    public string Value => _value ?? "default";

    private KyrolusCorsPolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom CORS policy with the specified policy name.
    /// </summary>
    /// <param name="policyName">The custom policy name registered in the CORS configuration.</param>
    public static KyrolusCorsPolicy Custom(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        return new(policyName.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusCorsPolicy"/> from a raw string value.
    /// </summary>
    public static KyrolusCorsPolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (string.Equals(trimmed, "default", StringComparison.OrdinalIgnoreCase)) return Default;
        if (string.Equals(trimmed, "disable", StringComparison.OrdinalIgnoreCase)) return Disable;
        return Custom(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusCorsPolicy"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusCorsPolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusCorsPolicy"/>.
    /// </summary>
    public static implicit operator KyrolusCorsPolicy(string? value) => From(value) ?? Default;
}
