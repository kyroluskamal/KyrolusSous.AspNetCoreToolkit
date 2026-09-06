namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that defends against HTTP Request Smuggling attacks (CWE-444 / RFC 7230 § 3.3.3 / RFC 9112 § 6.1)
/// by detecting conflicting or duplicate content length and transfer-encoding headers before proxying to backend services.
/// </summary>
public sealed class KyrolusRequestSmugglingTransformProvider : ITransformProvider
{
    private static readonly byte[] RequestSmugglingProblemBytes =
        """{"type":"https://httpstatuses.com/400","title":"Bad Request","status":400,"detail":"Conflicting or duplicate content transfer headers detected (HTTP Request Smuggling defense)."}"""u8.ToArray();

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the request smuggling detection transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var request = transformContext.HttpContext.Request;
            var headers = request.Headers;

            if (IsRequestSmugglingAttempt(headers))
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                transformContext.HttpContext.Response.ContentType = "application/problem+json";
                await transformContext.HttpContext.Response.Body.WriteAsync(
                    RequestSmugglingProblemBytes,
                    transformContext.HttpContext.RequestAborted);
            }
        });
    }

    /// <summary>
    /// Inspects the HTTP request headers for HTTP Request Smuggling anomalies.
    /// </summary>
    internal static bool IsRequestSmugglingAttempt(IHeaderDictionary headers)
    {
        // 1. Conflicting Transfer-Encoding AND Content-Length headers (CL.TE / TE.CL attack vector)
        var hasTransferEncoding = headers.TryGetValue("Transfer-Encoding", out var teValues) && teValues.Count > 0;
        var hasContentLength = headers.TryGetValue("Content-Length", out var clValues) && clValues.Count > 0;

        if (hasTransferEncoding && hasContentLength)
        {
            return true;
        }

        // 2. Multiple differing Content-Length headers (RFC 7230 § 3.3.2)
        if (hasContentLength && clValues.Count > 1)
        {
            var first = clValues[0]?.Trim();
            for (var i = 1; i < clValues.Count; i++)
            {
                if (!string.Equals(first, clValues[i]?.Trim(), StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        // 3. Obfuscated or invalid Transfer-Encoding headers (e.g. "chunked, chunked", "identity")
        if (hasTransferEncoding)
        {
            for (var i = 0; i < teValues.Count; i++)
            {
                var val = teValues[i];
                if (val is null) continue;

                if (val.Contains('\0') || val.Contains('\r') || val.Contains('\n'))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
