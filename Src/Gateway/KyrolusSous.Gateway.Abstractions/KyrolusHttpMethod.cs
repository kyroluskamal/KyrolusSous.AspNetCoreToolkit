namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed HTTP method identifier for gateway route matching and transforms.
/// Eliminates magic strings, ensures RFC 9110 compliance, and prevents runtime typos.
/// </summary>
public readonly record struct KyrolusHttpMethod : IEquatable<string>, IComparable<KyrolusHttpMethod>
{
    private readonly string? _value;

    /// <summary>
    /// Gets the normalized uppercase HTTP method verb (e.g. <c>"GET"</c>, <c>"POST"</c>).
    /// Defaults to <c>"GET"</c> if uninitialized.
    /// </summary>
    public string Value => _value ?? KyrolusGatewayHttpMethods.Get;

    /// <summary>HTTP GET method (RFC 9110).</summary>
    public static KyrolusHttpMethod Get { get; } = new(KyrolusGatewayHttpMethods.Get);

    /// <summary>HTTP POST method (RFC 9110).</summary>
    public static KyrolusHttpMethod Post { get; } = new(KyrolusGatewayHttpMethods.Post);

    /// <summary>HTTP PUT method (RFC 9110).</summary>
    public static KyrolusHttpMethod Put { get; } = new(KyrolusGatewayHttpMethods.Put);

    /// <summary>HTTP DELETE method (RFC 9110).</summary>
    public static KyrolusHttpMethod Delete { get; } = new(KyrolusGatewayHttpMethods.Delete);

    /// <summary>HTTP PATCH method (RFC 5789).</summary>
    public static KyrolusHttpMethod Patch { get; } = new(KyrolusGatewayHttpMethods.Patch);

    /// <summary>HTTP HEAD method (RFC 9110).</summary>
    public static KyrolusHttpMethod Head { get; } = new(KyrolusGatewayHttpMethods.Head);

    /// <summary>HTTP OPTIONS method (RFC 9110).</summary>
    public static KyrolusHttpMethod Options { get; } = new(KyrolusGatewayHttpMethods.Options);

    /// <summary>HTTP TRACE method (RFC 9110).</summary>
    public static KyrolusHttpMethod Trace { get; } = new(KyrolusGatewayHttpMethods.Trace);

    /// <summary>HTTP CONNECT method (RFC 9110).</summary>
    public static KyrolusHttpMethod Connect { get; } = new(KyrolusGatewayHttpMethods.Connect);

    /// <summary>
    /// Gets an immutable list of all 9 standard RFC HTTP methods.
    /// </summary>
    public static IReadOnlyList<KyrolusHttpMethod> AllStandardMethods { get; } =
    [
        Get, Post, Put, Delete, Patch, Head, Options, Trace, Connect
    ];

    private KyrolusHttpMethod(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a custom or extended HTTP method (e.g. <c>"PURGE"</c>, <c>"MERGE"</c>).
    /// </summary>
    /// <param name="customMethod">The custom HTTP method name.</param>
    public static KyrolusHttpMethod Custom(string customMethod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customMethod);
        return new(customMethod.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusHttpMethod"/> from a raw string value, matching standard verbs when possible.
    /// </summary>
    public static KyrolusHttpMethod? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().ToUpperInvariant();
        return trimmed switch
        {
            KyrolusGatewayHttpMethods.Get => Get,
            KyrolusGatewayHttpMethods.Post => Post,
            KyrolusGatewayHttpMethods.Put => Put,
            KyrolusGatewayHttpMethods.Delete => Delete,
            KyrolusGatewayHttpMethods.Patch => Patch,
            KyrolusGatewayHttpMethods.Head => Head,
            KyrolusGatewayHttpMethods.Options => Options,
            KyrolusGatewayHttpMethods.Trace => Trace,
            KyrolusGatewayHttpMethods.Connect => Connect,
            _ => Custom(trimmed)
        };
    }

    /// <summary>
    /// Attempts to parse an HTTP method string into a <see cref="KyrolusHttpMethod"/>.
    /// </summary>
    public static bool TryParse(string? input, out KyrolusHttpMethod method)
    {
        var resolved = From(input);
        if (resolved.HasValue)
        {
            method = resolved.Value;
            return true;
        }

        method = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(string? other) =>
        string.Equals(Value, other?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public int CompareTo(KyrolusHttpMethod other) =>
        string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusHttpMethod"/> to its string representation.
    /// </summary>
    public static implicit operator string(KyrolusHttpMethod method) => method.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="KyrolusHttpMethod"/>.
    /// </summary>
    public static implicit operator KyrolusHttpMethod(string? value) =>
        From(value) ?? Get;
}
