namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that enforces Content-Type whitelist filtering on gateway routes.
/// Defends against XXE, Deserialization, and unexpected payload attacks by rejecting unsupported media types with HTTP 415.
/// </summary>
public sealed class KyrolusContentTypeTransformProvider : ITransformProvider
{
    private static readonly byte[] UnsupportedMediaTypeBytes =
        """{"title":"Unsupported Media Type","status":415,"detail":"The request content type is not supported."}"""u8.ToArray();

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the Content-Type validation transform to the YARP transform pipeline if allowed content types are configured in route metadata.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;
        if (metadata is null || !metadata.TryGetValue("Kyrolus:ContentType:Allowed", out var allowedRaw) || string.IsNullOrWhiteSpace(allowedRaw))
        {
            return;
        }

        var allowedTypes = allowedRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (allowedTypes.Length == 0)
        {
            return;
        }

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var request = transformContext.HttpContext.Request;

            // Only check requests that have content
            var hasContent = (request.ContentLength is > 0) ||
                             request.Headers.ContainsKey("Transfer-Encoding");

            if (!hasContent)
            {
                return;
            }

            var contentType = request.ContentType;
            if (string.IsNullOrWhiteSpace(contentType) || !IsAllowedContentType(contentType, allowedTypes))
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                transformContext.HttpContext.Response.ContentType = "application/problem+json";
                await transformContext.HttpContext.Response.Body.WriteAsync(UnsupportedMediaTypeBytes, transformContext.HttpContext.RequestAborted);
            }
        });
    }

    private static bool IsAllowedContentType(string contentType, string[] allowedTypes)
    {
        // Strip charset or boundary if present (e.g., "application/json; charset=utf-8" -> "application/json")
        var mime = contentType.Split(';', StringSplitOptions.TrimEntries)[0];
        foreach (var allowed in allowedTypes)
        {
            if (string.Equals(mime, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
