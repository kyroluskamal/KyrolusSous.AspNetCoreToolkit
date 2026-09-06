namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Defines matching criteria for HTTP query string parameters on a gateway route.
/// </summary>
public sealed record KyrolusRouteQueryParameter
{
    /// <summary>
    /// Gets the name of the query string parameter to inspect.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the list of acceptable query parameter values.
    /// </summary>
    public IReadOnlyList<string>? Values { get; init; }

    /// <summary>
    /// Gets the comparison mode (<c>"Exact"</c>, <c>"Prefix"</c>, <c>"Exists"</c>, <c>"Contains"</c>, <c>"NotContains"</c>).
    /// Defaults to <c>"Exact"</c>.
    /// </summary>
    public string? Mode { get; init; } = "Exact";

    /// <summary>
    /// Gets whether the query parameter value comparison is case-sensitive. Defaults to <c>false</c>.
    /// </summary>
    public bool IsCaseSensitive { get; init; }
}
