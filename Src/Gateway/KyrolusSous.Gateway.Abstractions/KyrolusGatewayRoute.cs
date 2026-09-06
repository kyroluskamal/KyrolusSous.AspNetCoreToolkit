namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Represents an API Gateway routing rule that maps incoming client requests satisfying <see cref="Match"/> criteria
/// to a target backend service <see cref="ClusterId"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>How Routing Works:</b><br/>
/// A route acts as the bridge between external clients and internal backend microservices:
/// <list type="number">
/// <item><description>An external client makes an HTTP request to the Gateway.</description></item>
/// <item><description>The Gateway evaluates all configured routes against the request's path, method, and host using <see cref="Match"/>.</description></item>
/// <item><description>When a route matches, the Gateway forwards the request to one of the backend destinations inside the <see cref="ClusterId"/>.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var route = new KyrolusGatewayRoute
/// {
///     RouteId = "orders-api-route",
///     ClusterId = "orders-cluster",
///     Match = new KyrolusGatewayRouteMatch
///     {
///         Path = "/api/orders/{**catch-all}",
///         Methods = new[] { KyrolusGatewayHttpMethods.Get, KyrolusGatewayHttpMethods.Post }
///     },
///     Metadata = new Dictionary&lt;string, string&gt;
///     {
///         ["RateLimitPolicy"] = "strict",
///         ["RequiresAuth"] = "true"
///     }
/// };
/// </code>
/// </example>
public sealed record KyrolusGatewayRoute
{
    /// <summary>
    /// Gets the unique identifier for this routing rule (e.g., <c>"orders-route"</c>, <c>"billing-query-route"</c>).
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>
    /// Gets the identifier of the destination cluster to which matched requests will be forwarded.
    /// Must correspond to a defined <see cref="KyrolusGatewayCluster.ClusterId"/>.
    /// </summary>
    public required string ClusterId { get; init; }

    /// <summary>
    /// Gets the matching criteria (Path, HTTP Methods, Hosts) that inbound requests must satisfy.
    /// </summary>
    public required KyrolusGatewayRouteMatch Match { get; init; }

    /// <summary>
    /// Gets optional key-value metadata attached to this route.
    /// Can be used by custom transform providers, security middlewares, or rate limiters.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Gets the authorization policy name required to access this route.
    /// Unauthenticated or unauthorized callers receive an immediate 401 or 403 at the gateway edge.
    /// </summary>
    public string? AuthorizationPolicy { get; init; }

    /// <summary>
    /// Gets the CORS policy name applied to this route, handling browser preflight OPTIONS requests.
    /// </summary>
    public string? CorsPolicy { get; init; }

    /// <summary>
    /// Gets the ASP.NET Core rate limiter policy name applied to this route.
    /// Throttles abusive traffic at the gateway perimeter before reaching backend services.
    /// </summary>
    public string? RateLimiterPolicy { get; init; }

    /// <summary>
    /// Gets the timeout duration allocated for processing requests matching this route.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets the list of route-level request/response transforms (e.g. PathRemovePrefix, PathPrefix, PathSet).
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>>? Transforms { get; init; }
}
