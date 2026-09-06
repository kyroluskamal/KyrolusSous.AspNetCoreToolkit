namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed hostname/domain matching identifier for gateway routing rules.
/// Enforces RFC 1123, wildcard subdomain matching, and IP/port validation at construction.
/// </summary>
public readonly record struct KyrolusRouteHost : IEquatable<string>, IComparable<KyrolusRouteHost>
{
    private readonly string? _value;

    /// <summary>
    /// Gets the normalized lowercase hostname (e.g., <c>"api.example.com"</c>, <c>"*.example.com"</c>, or <c>"localhost:5000"</c>).
    /// Defaults to <c>"*"</c> (match all hosts) if uninitialized.
    /// </summary>
    public string Value => _value ?? "*";

    /// <summary>
    /// Catch-all wildcard host (<c>"*"</c>) that matches any inbound request domain.
    /// </summary>
    public static KyrolusRouteHost Any { get; } = new("*");

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusRouteHost"/> struct, validating the format via <see cref="KyrolusHostValidator"/>.
    /// </summary>
    /// <param name="host">The hostname or domain pattern to validate.</param>
    public KyrolusRouteHost(string host)
    {
        _value = KyrolusHostValidator.Validate(host);
    }

    /// <summary>
    /// Creates a validated <see cref="KyrolusRouteHost"/> from a raw string.
    /// </summary>
    public static KyrolusRouteHost From(string host) => new(host);

    /// <summary>
    /// Attempts to validate and parse a host string into a <see cref="KyrolusRouteHost"/>.
    /// </summary>
    public static bool TryParse(string? host, out KyrolusRouteHost result)
    {
        if (KyrolusHostValidator.TryValidate(host, out var normalized, out _))
        {
            result = new KyrolusRouteHost(normalized);
            return true;
        }

        result = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(string? other) =>
        string.Equals(Value, other?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public int CompareTo(KyrolusRouteHost other) =>
        string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusRouteHost"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusRouteHost host) => host.Value;

    /// <summary>
    /// Implicitly converts a string to a validated <see cref="KyrolusRouteHost"/>.
    /// </summary>
    public static implicit operator KyrolusRouteHost(string host) => new(host);
}
