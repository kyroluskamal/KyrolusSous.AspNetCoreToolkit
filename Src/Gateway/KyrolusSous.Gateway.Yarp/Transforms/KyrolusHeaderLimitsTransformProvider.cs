namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that defends against Header Buffer Overflow and Denial-of-Service attacks (CWE-400 / Slowloris)
/// by strictly validating the count and total byte size of inbound request headers before forwarding.
/// </summary>
public sealed class KyrolusHeaderLimitsTransformProvider : ITransformProvider
{
    private const int DefaultMaxHeaderCount = 100;
    private const int DefaultMaxTotalHeaderLength = 32768; // 32 KB

    private static readonly byte[] HeaderFieldsTooLargeBytes =
        """{"type":"https://httpstatuses.com/431","title":"Request Header Fields Too Large","status":431,"detail":"The request headers exceed the maximum allowable count or size limit."}"""u8.ToArray();

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        if (context.Route.Metadata is { } metadata)
        {
            if (metadata.TryGetValue("Kyrolus:Headers:MaxCount", out var maxCountRaw) &&
                !string.IsNullOrWhiteSpace(maxCountRaw) &&
                (!int.TryParse(maxCountRaw, out var parsedCount) || parsedCount <= 0))
            {
                context.Errors.Add(new ArgumentException($"Route '{context.Route.RouteId}' has invalid metadata 'Kyrolus:Headers:MaxCount' value '{maxCountRaw}'. Must be a positive integer."));
            }

            if (metadata.TryGetValue("Kyrolus:Headers:MaxTotalLength", out var maxLengthRaw) &&
                !string.IsNullOrWhiteSpace(maxLengthRaw) &&
                (!int.TryParse(maxLengthRaw, out var parsedLength) || parsedLength <= 0))
            {
                context.Errors.Add(new ArgumentException($"Route '{context.Route.RouteId}' has invalid metadata 'Kyrolus:Headers:MaxTotalLength' value '{maxLengthRaw}'. Must be a positive integer."));
            }
        }
    }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the request header limits transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;

        var maxCount = DefaultMaxHeaderCount;
        if (metadata != null &&
            metadata.TryGetValue("Kyrolus:Headers:MaxCount", out var maxCountRaw) &&
            int.TryParse(maxCountRaw, out var parsedCount) && parsedCount > 0)
        {
            maxCount = parsedCount;
        }

        var maxLength = DefaultMaxTotalHeaderLength;
        if (metadata != null &&
            metadata.TryGetValue("Kyrolus:Headers:MaxTotalLength", out var maxLengthRaw) &&
            int.TryParse(maxLengthRaw, out var parsedLength) && parsedLength > 0)
        {
            maxLength = parsedLength;
        }

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var headers = transformContext.HttpContext.Request.Headers;
            if (headers.Count > maxCount)
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status431RequestHeaderFieldsTooLarge;
                transformContext.HttpContext.Response.ContentType = "application/problem+json";
                await transformContext.HttpContext.Response.Body.WriteAsync(HeaderFieldsTooLargeBytes, transformContext.HttpContext.RequestAborted);
                return;
            }

            var totalLength = 0;
            foreach (var (key, val) in headers)
            {
                totalLength += key.Length + val.ToString().Length;
                if (totalLength > maxLength)
                {
                    transformContext.HttpContext.Response.StatusCode = StatusCodes.Status431RequestHeaderFieldsTooLarge;
                    transformContext.HttpContext.Response.ContentType = "application/problem+json";
                    await transformContext.HttpContext.Response.Body.WriteAsync(HeaderFieldsTooLargeBytes, transformContext.HttpContext.RequestAborted);
                    return;
                }
            }
        });
    }
}
