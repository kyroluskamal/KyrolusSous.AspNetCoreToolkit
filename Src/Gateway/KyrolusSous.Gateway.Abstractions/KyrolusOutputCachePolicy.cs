namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed output cache policy identifier for gateway routing rules.
/// Defines the response caching policy applied at the gateway edge.
/// </summary>
public readonly record struct KyrolusOutputCachePolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Gets the raw string policy name.
    /// </summary>
    public string Value => _value ?? string.Empty;

    private KyrolusOutputCachePolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom output cache policy with the specified policy name.
    /// </summary>
    /// <param name="policyName">The custom policy name registered in ASP.NET Core Output Caching.</param>
    public static KyrolusOutputCachePolicy Custom(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        return new(policyName.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusOutputCachePolicy"/> from a raw string value.
    /// </summary>
    public static KyrolusOutputCachePolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Custom(value);
    }

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusOutputCachePolicy"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusOutputCachePolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusOutputCachePolicy"/>.
    /// </summary>
    public static implicit operator KyrolusOutputCachePolicy(string? value) => From(value) ?? Custom(value ?? string.Empty);
}
