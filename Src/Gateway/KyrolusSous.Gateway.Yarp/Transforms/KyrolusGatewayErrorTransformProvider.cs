namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that intercepts upstream gateway failures (HTTP 502 Bad Gateway, 503 Service Unavailable, 504 Gateway Timeout)
/// and formats the response as a standardized RFC 9457 ProblemDetails JSON payload with zero-allocation UTF-8 literals.
/// </summary>
public sealed class KyrolusGatewayErrorTransformProvider : ITransformProvider
{
    private static readonly byte[] BadGatewayBytes =
        """{"title":"Bad Gateway","status":502,"detail":"The gateway failed to establish a connection to the upstream service or received an invalid response."}"""u8.ToArray();

    private static readonly byte[] ServiceUnavailableBytes =
        """{"title":"Service Unavailable","status":503,"detail":"The upstream service is currently unavailable or overloaded. Please try again later."}"""u8.ToArray();

    private static readonly byte[] GatewayTimeoutBytes =
        """{"title":"Gateway Timeout","status":504,"detail":"The upstream service did not respond within the configured gateway activity timeout."}"""u8.ToArray();

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the gateway error formatting response transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        context.AddResponseTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var statusCode = transformContext.HttpContext.Response.StatusCode;
            if (transformContext.ProxyResponse is { } proxyResponse)
            {
                statusCode = (int)proxyResponse.StatusCode;
            }

            byte[]? errorPayload = statusCode switch
            {
                StatusCodes.Status502BadGateway => BadGatewayBytes,
                StatusCodes.Status503ServiceUnavailable => ServiceUnavailableBytes,
                StatusCodes.Status504GatewayTimeout => GatewayTimeoutBytes,
                _ => null
            };

            if (errorPayload != null)
            {
                var response = transformContext.HttpContext.Response;
                response.StatusCode = statusCode;
                response.ContentType = "application/problem+json";
                response.ContentLength = errorPayload.Length;
                transformContext.SuppressResponseBody = true;
                await response.Body.WriteAsync(errorPayload, transformContext.HttpContext.RequestAborted);
            }
        });
    }
}
