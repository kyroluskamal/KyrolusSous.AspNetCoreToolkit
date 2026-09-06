namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Defines the matching criteria that an inbound client HTTP request must satisfy to trigger an API Gateway route.
/// </summary>
/// <remarks>
/// <para>
/// <b>How Route Matching Works:</b><br/>
/// When a request arrives from the internet or a client application, the Gateway inspects the request's URL path, HTTP method, and Host header:
/// <list type="bullet">
/// <item><description><b>Path</b>: Specifies the route template (e.g., <c>"/api/orders/{**catch-all}"</c> or <c>"/api/users/{id}"</c>).</description></item>
/// <item><description><b>Methods</b>: Optional filter for HTTP methods (e.g., <see cref="KyrolusGatewayHttpMethods.Get"/>, <see cref="KyrolusGatewayHttpMethods.Post"/>). If omitted, all verbs are matched.</description></item>
/// <item><description><b>Hosts</b>: Optional domain filter (e.g., <c>"api.example.com"</c>). This is the <i>client-facing domain</i>, distinct from internal cluster destination addresses.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var match = new KyrolusGatewayRouteMatch
/// {
///     Path = "/api/v1/invoices/{**catch-all}",
///     Methods = new[] { KyrolusGatewayHttpMethods.Get, KyrolusGatewayHttpMethods.Post },
///     Hosts = new[] { "api.mycompany.com" }
/// };
/// </code>
/// </example>
public sealed record KyrolusGatewayRouteMatch
{
    /// <summary>
    /// Gets the URL path pattern to match (e.g., <c>"/api/orders/{**catch-all}"</c>).
    /// Supports ASP.NET Core route template syntax including catch-all wildcards and route parameters.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the optional list of allowed HTTP methods (e.g., <c>"GET"</c>, <c>"POST"</c>).
    /// Recommended to use constants from <see cref="KyrolusGatewayHttpMethods"/>.
    /// If null or empty, the route matches all HTTP methods.
    /// </summary>
    public IReadOnlyList<string>? Methods { get; init; }

    /// <summary>
    /// Gets the optional list of client request hostnames (domains) to match (e.g., <c>"api.mycompany.com"</c>).
    /// If null or empty, the route matches requests on any host.
    /// </summary>
    public IReadOnlyList<string>? Hosts { get; init; }

    /// <summary>
    /// Gets the optional list of HTTP request header matching rules (e.g. for canary releases or API versioning).
    /// All specified header rules must match for the route to be selected.
    /// </summary>
    public IReadOnlyList<KyrolusRouteHeader>? Headers { get; init; }

    /// <summary>
    /// Gets the optional list of HTTP query string parameter matching rules.
    /// All specified query parameter rules must match for the route to be selected.
    /// </summary>
    public IReadOnlyList<KyrolusRouteQueryParameter>? QueryParameters { get; init; }
}
