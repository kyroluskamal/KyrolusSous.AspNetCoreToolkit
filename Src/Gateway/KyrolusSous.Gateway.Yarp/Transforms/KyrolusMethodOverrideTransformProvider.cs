namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that defends against HTTP Method Spoofing and Verb Tampering (CWE-287 / CWE-654)
/// by stripping untrusted method override headers or strictly validating them against declared route methods.
/// </summary>
public sealed class KyrolusMethodOverrideTransformProvider : ITransformProvider
{
    private static readonly byte[] MethodNotAllowedBytes =
        """{"type":"https://httpstatuses.com/405","title":"Method Not Allowed","status":405,"detail":"The specified HTTP method override is not allowed for this route."}"""u8.ToArray();

    private static readonly string[] OverrideHeaderNames =
    [
        "X-HTTP-Method-Override",
        "X-HTTP-Method",
        "X-Method-Override"
    ];

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        if (context.Route.Metadata?.TryGetValue("Kyrolus:MethodOverride:Allowed", out var val) == true &&
            !string.IsNullOrWhiteSpace(val) &&
            !bool.TryParse(val, out _))
        {
            context.Errors.Add(new ArgumentException($"Route '{context.Route.RouteId}' has invalid metadata 'Kyrolus:MethodOverride:Allowed' value '{val}'. Expected 'true' or 'false'."));
        }
    }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the method override security transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var allowOverride = IsOverrideAllowed(context.Route?.Metadata);
        var allowedMethods = context.Route?.Match.Methods;

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            // Defend against Cross-Site Tracing (XST - CWE-693) and unauthorized proxy tunneling
            if (IsDangerousVerb(transformContext.HttpContext.Request.Method))
            {
                await WriteMethodNotAllowedAsync(transformContext.HttpContext);
                return;
            }

            await ProcessMethodOverrideAsync(transformContext, allowOverride, allowedMethods);
        });
    }

    private static bool IsOverrideAllowed(IReadOnlyDictionary<string, string>? metadata) =>
        metadata != null &&
        metadata.TryGetValue("Kyrolus:MethodOverride:Allowed", out var val) &&
        bool.TryParse(val, out var isAllowed) && isAllowed;

    private static async ValueTask ProcessMethodOverrideAsync(
        RequestTransformContext transformContext,
        bool allowOverride,
        IReadOnlyList<string>? allowedMethods)
    {
        var overrideMethod = ExtractAndStripOverrideHeaders(transformContext);
        if (string.IsNullOrWhiteSpace(overrideMethod))
        {
            return;
        }

        overrideMethod = overrideMethod.Trim().ToUpperInvariant();

        if (IsDangerousVerb(overrideMethod) || (allowOverride && IsMethodDisallowed(overrideMethod, allowedMethods)))
        {
            await WriteMethodNotAllowedAsync(transformContext.HttpContext);
            return;
        }

        if (allowOverride)
        {
            transformContext.ProxyRequest.Method = new HttpMethod(overrideMethod);
        }
    }

    private static string? ExtractAndStripOverrideHeaders(RequestTransformContext transformContext)
    {
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
        return overrideMethod;
    }

    private static bool IsMethodDisallowed(string method, IReadOnlyList<string>? allowedMethods) =>
        allowedMethods is { Count: > 0 } && !allowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase);

    private static async Task WriteMethodNotAllowedAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.Body.WriteAsync(MethodNotAllowedBytes, httpContext.RequestAborted);
    }

    private static bool IsDangerousVerb(string method) =>
        string.Equals(method, "TRACE", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "TRACK", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase);
}
