namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that defends against HTTP Method Spoofing and Verb Tampering (CWE-287 / CWE-654)
/// by stripping untrusted method override headers or strictly validating them against declared route methods.
/// </summary>
public sealed class KyrolusMethodOverrideTransformProvider : ITransformProvider
{
    private static readonly byte[] MethodNotAllowedBytes =
        """{"title":"Method Not Allowed","status":405,"detail":"The specified HTTP method override is not allowed for this route."}"""u8.ToArray();

    private static readonly string[] OverrideHeaderNames =
    [
        "X-HTTP-Method-Override",
        "X-HTTP-Method",
        "X-Method-Override"
    ];

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the method override security transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;
        var allowOverride = metadata != null &&
                            metadata.TryGetValue("Kyrolus:MethodOverride:Allowed", out var val) &&
                            bool.TryParse(val, out var isAllowed) && isAllowed;

        var allowedMethods = context.Route?.Match.Methods;

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var request = transformContext.HttpContext.Request;

            string? overrideMethod = null;
            for (var i = 0; i < OverrideHeaderNames.Length; i++)
            {
                var hName = OverrideHeaderNames[i];
                if (request.Headers.TryGetValue(hName, out var values) && values.Count > 0)
                {
                    overrideMethod ??= values[0];
                    transformContext.ProxyRequest.Headers.Remove(hName);
                }
            }

            if (string.IsNullOrWhiteSpace(overrideMethod))
            {
                return;
            }

            overrideMethod = overrideMethod.Trim().ToUpperInvariant();

            if (!allowOverride)
            {
                return;
            }

            if (allowedMethods is { Count: > 0 } && !allowedMethods.Contains(overrideMethod, StringComparer.OrdinalIgnoreCase))
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                transformContext.HttpContext.Response.ContentType = "application/problem+json";
                await transformContext.HttpContext.Response.Body.WriteAsync(MethodNotAllowedBytes, transformContext.HttpContext.RequestAborted);
            }
        });
    }
}
