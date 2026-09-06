namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that enforces early request payload size limits (CWE-400 / CWE-770)
/// by evaluating incoming <c>Content-Length</c> headers against route limits and rejecting oversized requests with HTTP 413 Payload Too Large.
/// </summary>
public sealed class KyrolusPayloadSizeTransformProvider : ITransformProvider
{
    private static readonly byte[] PayloadTooLargeBytes =
        """{"title":"Payload Too Large","status":413,"detail":"The request body size exceeds the maximum permitted limit for this route."}"""u8.ToArray();

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the request body size limit transform to the YARP transform pipeline if a maximum body size is configured.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var maxBodySize = context.Route?.MaxRequestBodySize;

        if (maxBodySize is null && context.Route?.Metadata != null &&
            context.Route.Metadata.TryGetValue("Kyrolus:Payload:MaxSize", out var maxRaw) &&
            long.TryParse(maxRaw, out var parsedMax) && parsedMax > 0)
        {
            maxBodySize = parsedMax;
        }

        if (maxBodySize is null or <= 0)
        {
            return;
        }

        var limit = maxBodySize.Value;

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var contentLength = transformContext.HttpContext.Request.ContentLength;
            if (contentLength.HasValue && contentLength.Value > limit)
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                transformContext.HttpContext.Response.ContentType = "application/problem+json";
                await transformContext.HttpContext.Response.Body.WriteAsync(PayloadTooLargeBytes, transformContext.HttpContext.RequestAborted);
            }
        });
    }
}
