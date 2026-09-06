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
    public KyrolusAuthorizationPolicy? AuthorizationPolicy { get; init; }

    /// <summary>
    /// Gets the CORS policy name applied to this route, handling browser preflight OPTIONS requests.
    /// </summary>
    public KyrolusCorsPolicy? CorsPolicy { get; init; }

    /// <summary>
    /// Gets the ASP.NET Core rate limiter policy name applied to this route.
    /// Throttles abusive traffic at the gateway perimeter before reaching backend services.
    /// </summary>
    public KyrolusRateLimiterPolicy? RateLimiterPolicy { get; init; }

    /// <summary>
    /// Gets the ASP.NET Core output caching policy name applied to this route.
    /// Enables response caching at the gateway edge to reduce load on backend services.
    /// </summary>
    public KyrolusOutputCachePolicy? OutputCachePolicy { get; init; }

    /// <summary>
    /// Gets the timeout duration allocated for processing requests matching this route.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets the list of route-level request/response transforms (e.g. PathRemovePrefix, PathPrefix, PathSet).
    /// </summary>
    public IReadOnlyList<KyrolusGatewayTransform>? Transforms { get; init; }

    /// <summary>
    /// Gets the maximum allowed request body size in bytes for this route.
    /// Defends against denial-of-service (DoS) and memory exhaustion attacks via oversized payloads.
    /// </summary>
    public long? MaxRequestBodySize { get; init; }

    /// <summary>
    /// Gets the evaluation order priority for this route. Lower numerical values have higher matching precedence.
    /// Resolves ambiguity when multiple overlapping routes match the incoming request.
    /// </summary>
    public int? Order { get; init; }

    /// <summary>
    /// Gets optional IP allowlist and blocklist filtering options for this route.
    /// Requests from non-allowed or blocked IPs are rejected with HTTP 403 Forbidden.
    /// </summary>
    public KyrolusIpFilterOptions? IpFilter { get; init; }

    /// <summary>
    /// Gets whether this route strictly requires an authenticated multi-tenant context.
    /// If true and tenant resolution yields no valid tenant, the Gateway immediately rejects the request with HTTP 401 Unauthorized.
    /// </summary>
    public bool RequireTenant { get; init; }

    /// <summary>
    /// Gets the optional list of allowed HTTP request Content-Type MIME types (e.g. <c>"application/json"</c>).
    /// Requests with non-empty bodies bearing any other Content-Type are rejected at the edge with HTTP 415 Unsupported Media Type.
    /// </summary>
    public IReadOnlyList<string>? AllowedContentTypes { get; init; }
}
