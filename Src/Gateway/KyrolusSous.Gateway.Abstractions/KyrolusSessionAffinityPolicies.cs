namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed session affinity policy identifier.
/// Determines the mechanism used to maintain sticky sessions between clients and destination replicas.
/// </summary>
public readonly record struct KyrolusSessionAffinityPolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Maintains session affinity by issuing and reading an encrypted HTTP cookie.
    /// Recommended default for browser-based clients and web applications.
    /// </summary>
    public static KyrolusSessionAffinityPolicy Cookie { get; } = new("Cookie");

    /// <summary>
    /// Maintains session affinity using a custom HTTP request header.
    /// Ideal for native mobile apps, microservice-to-microservice calls, and REST clients.
    /// </summary>
    public static KyrolusSessionAffinityPolicy CustomHeader { get; } = new("CustomHeader");

    /// <summary>
    /// Gets the raw string policy name. Defaults to <c>"Cookie"</c>.
    /// </summary>
    public string Value => _value ?? "Cookie";

    private KyrolusSessionAffinityPolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom session affinity policy.
    /// </summary>
    public static KyrolusSessionAffinityPolicy Custom(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(name.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusSessionAffinityPolicy"/> from a raw string value.
    /// </summary>
    public static KyrolusSessionAffinityPolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (string.Equals(trimmed, "Cookie", StringComparison.OrdinalIgnoreCase)) return Cookie;
        if (string.Equals(trimmed, "CustomHeader", StringComparison.OrdinalIgnoreCase)) return CustomHeader;
        return Custom(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusSessionAffinityPolicy"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusSessionAffinityPolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusSessionAffinityPolicy"/>.
    /// </summary>
    public static implicit operator KyrolusSessionAffinityPolicy(string? policy) => From(policy) ?? Cookie;
}

/// <summary>
/// Strongly-typed session affinity failure policy identifier.
/// Controls how the reverse proxy handles requests when the affinitized destination replica becomes unavailable.
/// </summary>
public readonly record struct KyrolusSessionAffinityFailurePolicy : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Automatically redistributes the request to a different healthy destination in the cluster.
    /// Recommended default ensuring high availability.
    /// </summary>
    public static KyrolusSessionAffinityFailurePolicy Redistribute { get; } = new("Redistribute");

    /// <summary>
    /// Rejects the request with HTTP 503 Service Unavailable if the affinitized destination cannot be reached.
    /// Used when strict data locality or non-replicated in-memory state requires absolute single-node affinity.
    /// </summary>
    public static KyrolusSessionAffinityFailurePolicy Return503Error { get; } = new("Return503Error");

    /// <summary>
    /// Gets the raw string failure policy name. Defaults to <c>"Redistribute"</c>.
    /// </summary>
    public string Value => _value ?? "Redistribute";

    private KyrolusSessionAffinityFailurePolicy(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom session affinity failure policy.
    /// </summary>
    public static KyrolusSessionAffinityFailurePolicy Custom(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(name.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusSessionAffinityFailurePolicy"/> from a raw string value.
    /// </summary>
    public static KyrolusSessionAffinityFailurePolicy? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (string.Equals(trimmed, "Redistribute", StringComparison.OrdinalIgnoreCase)) return Redistribute;
        if (string.Equals(trimmed, "Return503Error", StringComparison.OrdinalIgnoreCase)) return Return503Error;
        return Custom(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusSessionAffinityFailurePolicy"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusSessionAffinityFailurePolicy policy) => policy.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusSessionAffinityFailurePolicy"/>.
    /// </summary>
    public static implicit operator KyrolusSessionAffinityFailurePolicy(string? policy) => From(policy) ?? Redistribute;
}
