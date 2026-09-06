namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Provides standard HTTP method constants for gateway route matching rules.
/// Eliminates magic strings and ensures compile-time safety and IntelliSense autocompletion.
/// </summary>
/// <remarks>
/// <para>
/// <b>Use in Route Matching:</b><br/>
/// By default, a gateway route matches all HTTP verbs unless constrained by specifying one or more allowed methods.
/// Passing these constants to <see cref="KyrolusGatewayRouteMatch.Methods"/> ensures strict verb isolation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Limiting a child route to GET and POST verbs:
/// cluster.AddRoute("invoices-query", "/api/invoices", KyrolusGatewayHttpMethods.Get, KyrolusGatewayHttpMethods.Post);
/// </code>
/// </example>
public static class KyrolusGatewayHttpMethods
{
    /// <summary>
    /// The HTTP GET method requests a representation of the specified resource.
    /// </summary>
    public const string Get = "GET";

    /// <summary>
    /// The HTTP POST method submits an entity to the specified resource.
    /// </summary>
    public const string Post = "POST";

    /// <summary>
    /// The HTTP PUT method replaces all current representations of the target resource with the request payload.
    /// </summary>
    public const string Put = "PUT";

    /// <summary>
    /// The HTTP DELETE method deletes the specified resource.
    /// </summary>
    public const string Delete = "DELETE";

    /// <summary>
    /// The HTTP PATCH method applies partial modifications to a resource.
    /// </summary>
    public const string Patch = "PATCH";

    /// <summary>
    /// The HTTP HEAD method asks for a response identical to a GET request, but without the response body.
    /// </summary>
    public const string Head = "HEAD";

    /// <summary>
    /// The HTTP OPTIONS method describes the communication options for the target resource.
    /// </summary>
    public const string Options = "OPTIONS";

    /// <summary>
    /// The HTTP TRACE method performs a message loop-back test along the path to the target resource.
    /// </summary>
    public const string Trace = "TRACE";

    /// <summary>
    /// The HTTP CONNECT method establishes a tunnel to the server identified by the target resource.
    /// </summary>
    public const string Connect = "CONNECT";
}
