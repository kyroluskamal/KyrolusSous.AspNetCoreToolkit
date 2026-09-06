namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed request header matching mode for gateway routing rules.
/// Defines how incoming request header values are compared against expected values.
/// </summary>
public readonly record struct KyrolusHeaderMatchMode : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Matches if the request header value equals the expected value exactly (case-insensitive by default).
    /// </summary>
    public static KyrolusHeaderMatchMode ExactHeader { get; } = new("ExactHeader");

    /// <summary>
    /// Matches if the request header value begins with the expected prefix string.
    /// </summary>
    public static KyrolusHeaderMatchMode HeaderPrefix { get; } = new("HeaderPrefix");

    /// <summary>
    /// Matches if the request header exists, regardless of its value.
    /// </summary>
    public static KyrolusHeaderMatchMode Exists { get; } = new("Exists");

    /// <summary>
    /// Matches if the request header is absent from the incoming request.
    /// </summary>
    public static KyrolusHeaderMatchMode NotExists { get; } = new("NotExists");

    /// <summary>
    /// Gets the raw string match mode value. Defaults to <c>"ExactHeader"</c>.
    /// </summary>
    public string Value => _value ?? "ExactHeader";

    private KyrolusHeaderMatchMode(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom header match mode with the specified mode name.
    /// </summary>
    public static KyrolusHeaderMatchMode Custom(string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        return new(mode.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusHeaderMatchMode"/> from a raw string value.
    /// </summary>
    public static KyrolusHeaderMatchMode? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "ExactHeader", StringComparison.OrdinalIgnoreCase)) return ExactHeader;
        if (string.Equals(trimmed, "HeaderPrefix", StringComparison.OrdinalIgnoreCase)) return HeaderPrefix;
        if (string.Equals(trimmed, "Exists", StringComparison.OrdinalIgnoreCase)) return Exists;
        if (string.Equals(trimmed, "NotExists", StringComparison.OrdinalIgnoreCase)) return NotExists;

        return Custom(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusHeaderMatchMode"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusHeaderMatchMode mode) => mode.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusHeaderMatchMode"/>.
    /// </summary>
    public static implicit operator KyrolusHeaderMatchMode(string? mode) => From(mode) ?? ExactHeader;
}
