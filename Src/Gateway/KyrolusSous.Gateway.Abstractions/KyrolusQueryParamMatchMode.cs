namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed query string parameter matching mode for gateway routing rules.
/// Defines how incoming query parameter values are evaluated against expected criteria.
/// </summary>
public readonly record struct KyrolusQueryParamMatchMode : IEquatable<string>
{
    private readonly string? _value;

    /// <summary>
    /// Matches if the query parameter value matches the expected value exactly.
    /// </summary>
    public static KyrolusQueryParamMatchMode Exact { get; } = new("Exact");

    /// <summary>
    /// Matches if the query parameter value begins with the expected prefix string.
    /// </summary>
    public static KyrolusQueryParamMatchMode Prefix { get; } = new("Prefix");

    /// <summary>
    /// Matches if the query parameter exists on the incoming request, regardless of its value.
    /// </summary>
    public static KyrolusQueryParamMatchMode Exists { get; } = new("Exists");

    /// <summary>
    /// Matches if the query parameter value contains the expected substring.
    /// </summary>
    public static KyrolusQueryParamMatchMode Contains { get; } = new("Contains");

    /// <summary>
    /// Matches if the query parameter value does NOT contain the expected substring.
    /// </summary>
    public static KyrolusQueryParamMatchMode NotContains { get; } = new("NotContains");

    /// <summary>
    /// Gets the raw string match mode value. Defaults to <c>"Exact"</c>.
    /// </summary>
    public string Value => _value ?? "Exact";

    private KyrolusQueryParamMatchMode(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom query parameter match mode with the specified mode name.
    /// </summary>
    public static KyrolusQueryParamMatchMode Custom(string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        return new(mode.Trim());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusQueryParamMatchMode"/> from a raw string value.
    /// </summary>
    public static KyrolusQueryParamMatchMode? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "Exact", StringComparison.OrdinalIgnoreCase)) return Exact;
        if (string.Equals(trimmed, "Prefix", StringComparison.OrdinalIgnoreCase)) return Prefix;
        if (string.Equals(trimmed, "Exists", StringComparison.OrdinalIgnoreCase)) return Exists;
        if (string.Equals(trimmed, "Contains", StringComparison.OrdinalIgnoreCase)) return Contains;
        if (string.Equals(trimmed, "NotContains", StringComparison.OrdinalIgnoreCase)) return NotContains;

        return Custom(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusQueryParamMatchMode"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusQueryParamMatchMode mode) => mode.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusQueryParamMatchMode"/>.
    /// </summary>
    public static implicit operator KyrolusQueryParamMatchMode(string? mode) => From(mode) ?? Exact;
}
