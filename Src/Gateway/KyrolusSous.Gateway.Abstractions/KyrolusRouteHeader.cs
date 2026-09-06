namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Defines matching criteria for HTTP request headers on a gateway route.
/// Enables canary releases, API versioning, and client-type routing at the gateway edge.
/// </summary>
public sealed record KyrolusRouteHeader
{
    /// <summary>
    /// Gets the name of the request header to match (e.g. <c>"X-API-Version"</c> or <c>"Accept"</c>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the list of acceptable header values. The route matches if any of these values match the request header.
    /// </summary>
    public IReadOnlyList<string>? Values { get; init; }

    /// <summary>
    /// Gets the comparison mode (<c>"ExactHeader"</c>, <c>"HeaderPrefix"</c>, <c>"Exists"</c>, <c>"NotExists"</c>).
    /// Defaults to <c>"ExactHeader"</c>.
    /// </summary>
    public string? Mode { get; init; } = "ExactHeader";

    /// <summary>
    /// Gets whether the header value comparison is case-sensitive. Defaults to <c>false</c>.
    /// </summary>
    public bool IsCaseSensitive { get; init; }
}
